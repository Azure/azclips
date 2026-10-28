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
/// Offline tests for <see cref="ListVMProcessor"/>. The ARM list endpoint returns an envelope
/// <c>{ "value": [...] }</c>; the processor unwraps it and writes the flat VM array (regression:
/// it previously deserialized the envelope into a single VM, so non-empty resource groups
/// appeared to contain no VMs). Pagination is intentionally not followed yet.
/// </summary>
public class ListVMProcessorTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    private static ListVMProcessor CreateProcessor(FakeHttpPipeline pipeline, FakeOutputHandler output)
    {
        var processor = new ListVMProcessor { SubscriptionId = TestSubscriptionId };
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
}
