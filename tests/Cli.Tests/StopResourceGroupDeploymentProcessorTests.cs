// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for <see cref="StopResourceGroupDeploymentProcessor"/>: the cancel POST targets the
/// escaped ARM URI at the pinned api-version, an ARM 204 No Content leaves the command silent, and a
/// non-success status becomes a terminating failure. Keeps the endpoint and the silent-success
/// contract from regressing without a live subscription.
/// </summary>
public class StopResourceGroupDeploymentProcessorTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    private static StopResourceGroupDeploymentProcessor CreateProcessor(
        FakeHttpPipeline pipeline,
        FakeOutputHandler output)
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var processor = new StopResourceGroupDeploymentProcessor();
        processor.Init(new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile));
        processor.SubscriptionId = TestSubscriptionId;
        return processor;
    }

    private static HttpResponseMessage NoContent() => new(HttpStatusCode.NoContent);

    [Fact]
    public async Task ProcessAsync_PostsCancelUriWithEscapedSegments()
    {
        HttpMethod? sentMethod = null;
        Uri? sentUri = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentMethod = request.Method;
            sentUri = request.RequestUri;
            return NoContent();
        });
        var output = new FakeOutputHandler();
        StopResourceGroupDeploymentProcessor processor = CreateProcessor(pipeline, output);
        processor.ResourceGroupName = "my rg";
        processor.Name = "dep#1";

        await processor.ProcessAsync();

        Assert.Equal(HttpMethod.Post, sentMethod);
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourcegroups/my%20rg"
                + "/providers/Microsoft.Resources/deployments/dep%231/cancel?api-version=2025-04-01",
            sentUri!.AbsoluteUri);
    }

    [Fact]
    public async Task ProcessAsync_NoContentResponseWritesNoOutput()
    {
        var pipeline = new FakeHttpPipeline((_, _) => NoContent());
        var output = new FakeOutputHandler();
        StopResourceGroupDeploymentProcessor processor = CreateProcessor(pipeline, output);
        processor.ResourceGroupName = "my-rg";
        processor.Name = "dep1";

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task ProcessAsync_FailureStatusThrowsAndWritesNoOutput()
    {
        const string body = """
            {
              "error": {
                "code": "DeploymentCannotBeCancelled",
                "message": "Deployment 'dep1' cannot be cancelled in the 'Succeeded' state."
              }
            }
            """;
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
        var output = new FakeOutputHandler();
        StopResourceGroupDeploymentProcessor processor = CreateProcessor(pipeline, output);
        processor.ResourceGroupName = "my-rg";
        processor.Name = "dep1";

        HttpRequestException failure = await Assert.ThrowsAsync<HttpRequestException>(processor.ProcessAsync);

        Assert.Equal(HttpStatusCode.Conflict, failure.StatusCode);
        Assert.Contains("DeploymentCannotBeCancelled", failure.Message);
        Assert.Empty(output.WrittenValues);
    }
}
