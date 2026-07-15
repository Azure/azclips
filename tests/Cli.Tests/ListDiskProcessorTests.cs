// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Microsoft.Azure.Azclips.Extensions.Compute.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for <see cref="ListDiskProcessor"/>. Same regression class as
/// <see cref="ListVMProcessorTests"/>: the ARM <c>disks</c> list endpoint returns an envelope
/// <c>{ "value": [...], "nextLink": "..." }</c> that must be unwrapped into a flat array.
/// </summary>
public class ListDiskProcessorTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    private static ListDiskProcessor CreateProcessor(FakeHttpPipeline pipeline, FakeOutputHandler output)
    {
        var processor = new ListDiskProcessor();
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var context = new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile);
        processor.Init(context);
        processor.ResourceGroupName = "my-rg";
        return processor;
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ProcessAsync_UnwrapsValueEnvelope_IntoFlatDiskArray()
    {
        const string body = """
            {
              "value": [
                { "id": "/subscriptions/s/resourceGroups/my-rg/providers/Microsoft.Compute/disks/disk1", "name": "disk1", "location": "eastus" },
                { "id": "/subscriptions/s/resourceGroups/my-rg/providers/Microsoft.Compute/disks/disk2", "name": "disk2", "location": "westus" }
              ]
            }
            """;
        var pipeline = new FakeHttpPipeline((_, _) => Json(body));
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        var disks = Assert.IsType<Disk[]>(Assert.Single(output.WrittenValues));
        Assert.Equal(new[] { "disk1", "disk2" }, disks.Select(d => d.Name));
    }

    [Fact]
    public async Task ProcessAsync_EmptyValue_WritesEmptyArray()
    {
        var pipeline = new FakeHttpPipeline((_, _) => Json("""{ "value": [] }"""));
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);

        await processor.ProcessAsync();

        var disks = Assert.IsType<Disk[]>(Assert.Single(output.WrittenValues));
        Assert.Empty(disks);
    }

    [Fact]
    public async Task ProcessAsync_FollowsNextLink_AcrossPages()
    {
        const string page1 = """
            {
              "value": [ { "name": "disk1", "location": "eastus" } ],
              "nextLink": "https://management.azure.com/subscriptions/s/resourceGroups/my-rg/providers/Microsoft.Compute/disks?api-version=2025-01-02&%24skiptoken=page2"
            }
            """;
        const string page2 = """{ "value": [ { "name": "disk2", "location": "westus" } ] }""";

        var pipeline = new FakeHttpPipeline((request, _) =>
            request.RequestUri!.Query.Contains("skiptoken", StringComparison.OrdinalIgnoreCase)
                ? Json(page2)
                : Json(page1));
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(2, pipeline.SendCount);
        var disks = Assert.IsType<Disk[]>(Assert.Single(output.WrittenValues));
        Assert.Equal(new[] { "disk1", "disk2" }, disks.Select(d => d.Name));
    }
}
