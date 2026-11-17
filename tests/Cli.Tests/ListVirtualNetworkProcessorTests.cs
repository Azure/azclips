// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Microsoft.Azure.Azclips.Extensions.Network.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for <see cref="ListAllVirtualNetworksProcessor_Orchestrator"/>: the ARM list endpoint returns an
/// envelope <c>{ "value": [...] }</c> that must be unwrapped into a flat array, and the request
/// URI must switch between the resource-group and subscription scopes.
/// </summary>
public class ListVirtualNetworkProcessorTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    private static ListAllVirtualNetworksProcessor_Orchestrator CreateProcessor(FakeHttpPipeline pipeline, FakeOutputHandler output, string resourceGroupName)
    {
        var processor = new ListAllVirtualNetworksProcessor_Orchestrator();
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var context = new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile);
        processor.Init(context);
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroupName = resourceGroupName;
        return processor;
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ProcessAsync_UnwrapsValueEnvelope_IntoFlatVnetArray()
    {
        const string body = """
            {
              "value": [
                { "id": "/subscriptions/s/resourceGroups/my-rg/providers/Microsoft.Network/virtualNetworks/vnet1", "name": "vnet1", "location": "eastus" },
                { "id": "/subscriptions/s/resourceGroups/my-rg/providers/Microsoft.Network/virtualNetworks/vnet2", "name": "vnet2", "location": "westus" }
              ]
            }
            """;
        var pipeline = new FakeHttpPipeline((_, _) => Json(body));
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output, "my-rg");

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        var written = Assert.Single(output.WrittenValues);
        var vnets = Assert.IsType<List<VirtualNetwork>>(written);
        Assert.Equal(2, vnets.Count);
        Assert.Equal("vnet1", vnets[0].Name);
        Assert.Equal("vnet2", vnets[1].Name);
    }

    [Fact]
    public async Task ProcessAsync_EmptyValue_WritesEmptyArray()
    {
        var pipeline = new FakeHttpPipeline((_, _) => Json("""{ "value": [] }"""));
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output, "my-rg");

        await processor.ProcessAsync();

        var written = Assert.Single(output.WrittenValues);
        var vnets = Assert.IsType<List<VirtualNetwork>>(written);
        Assert.Empty(vnets);
    }

    [Theory]
    [InlineData("my-rg", "/resourceGroups/my-rg/providers/Microsoft.Network/virtualNetworks")]
    [InlineData("", "/providers/Microsoft.Network/virtualNetworks")]
    public async Task ProcessAsync_ScopesRequestUri_ByResourceGroupPresence(string resourceGroupName, string expectedPathFragment)
    {
        Uri? requested = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requested = request.RequestUri;
            return Json("""{ "value": [] }""");
        });
        var processor = CreateProcessor(pipeline, new FakeOutputHandler(), resourceGroupName);

        await processor.ProcessAsync();

        Assert.NotNull(requested);
        Assert.Contains($"/subscriptions/{TestSubscriptionId}{expectedPathFragment}", requested!.ToString());
        if (resourceGroupName.Length == 0)
        {
            Assert.DoesNotContain("/resourceGroups/", requested.ToString());
        }
    }

    [Fact]
    public async Task ProcessAsync_FollowsNextLink_AndWritesEveryPage()
    {
        const string page2 = "https://management.azure.com/page2";
        var sentUris = new List<string>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUris.Add(request.RequestUri!.ToString());
            return sentUris.Count == 1
                ? Json($$"""{ "value": [ { "name": "vnet1" }, { "name": "vnet2" } ], "nextLink": "{{page2}}" }""")
                : Json("""{ "value": [ { "name": "vnet3" } ] }""");
        });
        var output = new FakeOutputHandler();
        var processor = CreateProcessor(pipeline, output, "my-rg");

        await processor.ProcessAsync();

        Assert.Equal(page2, sentUris[1]);
        var vnets = Assert.IsType<List<VirtualNetwork>>(Assert.Single(output.WrittenValues));
        Assert.Equal(["vnet1", "vnet2", "vnet3"], vnets.Select(vnet => vnet.Name!).ToArray());
    }
}
