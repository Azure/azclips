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
/// Offline tests for <see cref="GetResourceGroupDeploymentProcessor"/>: the request URI escapes the
/// resource-group and deployment-name segments, and the ARM response deserializes into
/// <see cref="DeploymentExtended"/>. Keeps URL/response regressions off a live subscription.
/// </summary>
public class GetResourceGroupDeploymentProcessorTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    private static GetResourceGroupDeploymentProcessor CreateProcessor(
        FakeHttpPipeline pipeline,
        FakeOutputHandler output)
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var processor = new GetResourceGroupDeploymentProcessor();
        processor.Init(new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile));
        processor.SubscriptionId = TestSubscriptionId;
        return processor;
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ProcessAsync_EscapesResourceGroupAndDeploymentNameSegments()
    {
        HttpMethod? sentMethod = null;
        Uri? sentUri = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentMethod = request.Method;
            sentUri = request.RequestUri;
            return Json("""{ "name": "dep#1" }""");
        });
        var output = new FakeOutputHandler();
        GetResourceGroupDeploymentProcessor processor = CreateProcessor(pipeline, output);
        processor.ResourceGroupName = "my rg";
        processor.Name = "dep#1";

        await processor.ProcessAsync();

        Assert.Equal(HttpMethod.Get, sentMethod);
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourcegroups/my%20rg"
                + "/providers/Microsoft.Resources/deployments/dep%231?api-version=2025-04-01",
            sentUri!.AbsoluteUri);
    }

    [Fact]
    public async Task ProcessAsync_DeserializesDeploymentExtendedResponse()
    {
        const string body = """
            {
              "id": "/subscriptions/s/resourceGroups/my-rg/providers/Microsoft.Resources/deployments/dep1",
              "name": "dep1",
              "type": "Microsoft.Resources/deployments",
              "properties": {
                "provisioningState": "Succeeded",
                "mode": "Incremental",
                "timestamp": "2026-09-08T01:02:03Z",
                "duration": "PT1M2S",
                "outputResources": [
                  { "id": "/subscriptions/s/resourceGroups/my-rg/providers/Microsoft.Storage/storageAccounts/sa1" }
                ]
              }
            }
            """;
        var pipeline = new FakeHttpPipeline((_, _) => Json(body));
        var output = new FakeOutputHandler();
        GetResourceGroupDeploymentProcessor processor = CreateProcessor(pipeline, output);
        processor.ResourceGroupName = "my-rg";
        processor.Name = "dep1";

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(output.WrittenValues));
        Assert.Equal("dep1", deployment.Name);
        Assert.Equal("Microsoft.Resources/deployments", deployment.Type);
        Assert.Equal("Succeeded", deployment.Properties!.ProvisioningState);
        Assert.Equal(DeploymentMode.Incremental, deployment.Properties.Mode);
        Assert.Equal("PT1M2S", deployment.Properties.Duration);
        Assert.Equal(
            "/subscriptions/s/resourceGroups/my-rg/providers/Microsoft.Storage/storageAccounts/sa1",
            Assert.Single(deployment.Properties.OutputResources!).Id);
    }
}
