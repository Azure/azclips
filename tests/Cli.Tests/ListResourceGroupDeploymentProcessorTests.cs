// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for <see cref="ListResourceGroupDeploymentProcessor"/>. The ARM list endpoint returns
/// an envelope <c>{ "value": [...], "nextLink": "..." }</c>; the processor unwraps it, follows
/// <c>nextLink</c>, and writes the flat deployment list. Also covers the empty-page and
/// no-body responses, where nothing may be dereferenced off a null envelope.
/// </summary>
public class ListResourceGroupDeploymentProcessorTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    private static ListResourceGroupDeploymentProcessor CreateProcessor(
        FakeHttpPipeline pipeline,
        FakeOutputHandler output)
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var processor = new ListResourceGroupDeploymentProcessor();
        processor.Init(new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile));
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroupName = "my-rg";
        return processor;
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ProcessAsync_UnwrapsValueEnvelope_IntoFlatDeploymentList()
    {
        const string body = """
            {
              "value": [
                { "name": "dep1", "properties": { "provisioningState": "Succeeded", "mode": "Incremental" } },
                { "name": "dep2", "properties": { "provisioningState": "Failed", "mode": "Complete" } }
              ]
            }
            """;
        var pipeline = new FakeHttpPipeline((_, _) => Json(body));
        var output = new FakeOutputHandler();
        ListResourceGroupDeploymentProcessor processor = CreateProcessor(pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        var deployments = Assert.IsType<List<DeploymentExtended>>(Assert.Single(output.WrittenValues));
        Assert.Equal(2, deployments.Count);
        Assert.Equal("dep1", deployments[0].Name);
        Assert.Equal("Succeeded", deployments[0].Properties!.ProvisioningState);
        Assert.Equal(DeploymentMode.Complete, deployments[1].Properties!.Mode);
    }

    [Fact]
    public async Task ProcessAsync_FollowsNextLink_AndConcatenatesPages()
    {
        const string nextLink = "https://management.azure.com/next-page?api-version=2025-04-01&skipToken=abc";
        var requestedUris = new List<string>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requestedUris.Add(request.RequestUri!.AbsoluteUri);
            return requestedUris.Count == 1
                ? Json($$"""{ "value": [ { "name": "dep1" } ], "nextLink": "{{nextLink}}" }""")
                : Json("""{ "value": [ { "name": "dep2" }, { "name": "dep3" } ] }""");
        });
        var output = new FakeOutputHandler();
        ListResourceGroupDeploymentProcessor processor = CreateProcessor(pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(2, pipeline.SendCount);
        Assert.Equal(nextLink, requestedUris[1]);
        var deployments = Assert.IsType<List<DeploymentExtended>>(Assert.Single(output.WrittenValues));
        Assert.Equal(new[] { "dep1", "dep2", "dep3" }, deployments.Select(d => d.Name).ToArray());
    }

    [Fact]
    public async Task ProcessAsync_EmptyValue_WritesEmptyList()
    {
        var pipeline = new FakeHttpPipeline((_, _) => Json("""{ "value": [] }"""));
        var output = new FakeOutputHandler();
        ListResourceGroupDeploymentProcessor processor = CreateProcessor(pipeline, output);

        await processor.ProcessAsync();

        Assert.Empty(Assert.IsType<List<DeploymentExtended>>(Assert.Single(output.WrittenValues)));
    }

    [Fact]
    public async Task ProcessAsync_EmptyResponseBody_WritesEmptyList()
    {
        var pipeline = new FakeHttpPipeline((_, _) => Json(string.Empty));
        var output = new FakeOutputHandler();
        ListResourceGroupDeploymentProcessor processor = CreateProcessor(pipeline, output);

        await processor.ProcessAsync();

        Assert.Empty(Assert.IsType<List<DeploymentExtended>>(Assert.Single(output.WrittenValues)));
    }
}
