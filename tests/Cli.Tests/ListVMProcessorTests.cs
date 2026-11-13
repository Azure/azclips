// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Microsoft.Azure.Azclips.Extensions.Compute.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for <see cref="ListVMProcessor"/>. The ARM list endpoint returns an envelope
/// <c>{ "value": [...] }</c>; the processor unwraps it and writes the flat VM array (regression:
/// it previously deserialized the envelope into a single VM, so non-empty resource groups
/// appeared to contain no VMs). Also covers paged execution and post-collection enrichment.
/// </summary>
public class ListVMProcessorTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    private static ListVMProcessor CreateProcessor(FakeHttpPipeline pipeline, FakeOutputHandler output) =>
        CreateProcessor(new ListVMProcessor(), pipeline, output);

    private static T CreateProcessor<T>(T processor, FakeHttpPipeline pipeline, FakeOutputHandler output, FakeEventPublisher? publisher = null)
        where T : ListVMProcessor
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var context = new ProcessorContext(
            pipeline,
            publisher ?? new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile);
        processor.Init(context);
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroupName = "my-rg";
        return processor;
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ProcessAsync_UnwrapsValueEnvelope_IntoFlatVmArray()
    {
        const string body = """
            {
              "value": [
                { "id": "/subscriptions/s/resourceGroups/my-rg/providers/Microsoft.Compute/virtualMachines/vm1", "name": "vm1", "location": "eastus" },
                { "id": "/subscriptions/s/resourceGroups/my-rg/providers/Microsoft.Compute/virtualMachines/vm2", "name": "vm2", "location": "westus" }
              ]
            }
            """;
        var pipeline = new FakeHttpPipeline((_, _) => Json(body));
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        var written = Assert.Single(output.WrittenValues);
        var vms = Assert.IsType<List<VirtualMachine>>(written);
        Assert.Equal(2, vms.Count);
        Assert.Equal("vm1", vms[0].Name);
        Assert.Equal("vm2", vms[1].Name);
    }

    [Fact]
    public async Task ProcessAsync_EmptyValue_WritesEmptyArray()
    {
        var pipeline = new FakeHttpPipeline((_, _) => Json("""{ "value": [] }"""));
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);

        await processor.ProcessAsync();

        var written = Assert.Single(output.WrittenValues);
        var vms = Assert.IsType<List<VirtualMachine>>(written);
        Assert.Empty(vms);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessAsync_CollectsAllPagesBeforeWriting(bool emptyFirstPage)
    {
        const string nextLink = "https://management.azure.com/next-page";
        var sent = new List<string>();
        var output = new FakeOutputHandler();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            Assert.Empty(output.WrittenValues);
            sent.Add(request.RequestUri!.AbsoluteUri);
            return request.RequestUri.AbsoluteUri == nextLink
                ? Json("""{"value":[{"name":"second"}]}""")
                : Json($$"""{"value":{{(emptyFirstPage ? "[]" : "[{\"name\":\"first\"}]")}},"nextLink":"{{nextLink}}"}""");
        });
        var processor = CreateProcessor(pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(2, sent.Count);
        Assert.Equal(nextLink, sent[1]);
        var vms = Assert.IsType<List<VirtualMachine>>(Assert.Single(output.WrittenValues));
        Assert.Equal(emptyFirstPage ? ["second"] : ["first", "second"], vms.Select(vm => vm.Name));
    }

    [Fact]
    public async Task PageIteratorDoesNotFetchUntilAdvancedAndCanStopAfterOnePage()
    {
        var pipeline = new FakeHttpPipeline((_, _) =>
            Json("""{"value":[{"name":"first"}],"nextLink":"https://management.azure.com/next-page"}"""));
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(new InspectableListProcessor(), pipeline, output);
        IAsyncEnumerable<VirtualMachineListResult> pages = processor.Pages();

        Assert.Equal(0, pipeline.SendCount);
        await foreach (VirtualMachineListResult page in pages)
        {
            Assert.Equal("first", Assert.Single(page.Value!).Name);
            break;
        }

        Assert.Equal(1, pipeline.SendCount);
        Assert.Empty(output.WrittenValues);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrchestratorCollectsAllPagesThenFiltersBeforeEnrichment(bool matchOnFirstPage)
    {
        const string nextLink = "https://management.azure.com/next-page";
        string targetId = $"/subscriptions/{TestSubscriptionId}/resourceGroups/my-rg/providers/Microsoft.Compute/virtualMachines/target";
        var sent = new List<string>();
        var output = new FakeOutputHandler();
        var publisher = new FakeEventPublisher();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            Assert.Empty(output.WrittenValues);
            string uri = request.RequestUri!.AbsoluteUri;
            sent.Add(uri);
            if (request.RequestUri.AbsolutePath.EndsWith("/virtualMachines/target", StringComparison.Ordinal))
            {
                Assert.Equal(3, sent.Count);
                Assert.Contains("$expand=instanceView", uri);
                return Json($$"""
                    {"id":"{{targetId}}","name":"target","properties":{"instanceView":{
                        "osName":"test-os","statuses":[{"code":"PowerState/running","displayStatus":"VM running"}] } } }
                    """);
            }

            bool firstPage = uri != nextLink;
            string name = firstPage == matchOnFirstPage ? "target" : "discarded";
            string id = targetId.Replace("/target", "/" + name);
            return Json(JsonSerializer.Serialize(new
            {
                value = new[] { new { id, name } },
                nextLink = firstPage ? nextLink : null,
            }));
        });
        var processor = CreateProcessor(new HookObservingOrchestrator(), pipeline, output, publisher);
        processor.Name = "TaRgEt";
        processor.Status = true;

        await processor.ProcessAsync();

        Assert.Equal(3, sent.Count);
        Assert.Equal(nextLink, sent[1]);
        VirtualMachine vm = Assert.Single(Assert.IsType<List<VirtualMachine>>(Assert.Single(output.WrittenValues)));
        Assert.Equal("target", vm.Name);
        Assert.Equal("VM running", vm.PowerState);
        Assert.Equal("test-os", vm.OsName);
        Assert.Null(vm.Properties?.InstanceView);
        Assert.False(processor.OutputHookCalled);
        Assert.Equal(1, publisher.PublishedIds.Count(id => id == Events.ProcessorProcessAsyncStart));
        Assert.Equal(1, publisher.PublishedIds.Count(id => id == Events.ProcessorProcessAsyncEnd));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LaterPageFailureDoesNotWriteOrEnrichPartialResults(bool orchestrated)
    {
        const string nextLink = "https://management.azure.com/next-page";
        var pipeline = new FakeHttpPipeline((request, _) => request.RequestUri!.AbsoluteUri == nextLink
            ? new HttpResponseMessage(HttpStatusCode.Forbidden)
            : Json($$"""{"value":[{"name":"first"}],"nextLink":"{{nextLink}}"}"""));
        var output = new FakeOutputHandler();
        var publisher = new FakeEventPublisher();
        ListVMProcessor processor = orchestrated
            ? new ListVMProcessor_Orchestrator { Status = true }
            : new ListVMProcessor();
        CreateProcessor(processor, pipeline, output, publisher);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
        Assert.Equal(2, pipeline.SendCount);
        Assert.Empty(output.WrittenValues);
        Assert.Equal(1, publisher.PublishedIds.Count(id => id == Events.ProcessorProcessAsyncEnd));
    }

    private sealed class InspectableListProcessor : ListVMProcessor
    {
        public IAsyncEnumerable<VirtualMachineListResult> Pages() => ReadPagesAsync();
    }

    private sealed class HookObservingOrchestrator : ListVMProcessor_Orchestrator
    {
        public bool OutputHookCalled { get; private set; }

        protected override Task<bool> BeforeWriteOutputAsyncAndReturn(VirtualMachineListResult? body)
        {
            OutputHookCalled = true;
            return Task.FromResult(false);
        }
    }
}
