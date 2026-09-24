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
/// Offline tests for <see cref="RemoveResourceGroupDeploymentProcessor"/>: the delete DELETEs the
/// escaped ARM URI at the pinned api-version, follows the Location header until the long-running
/// delete reports 204, never issues a final GET against the deleted deployment, and stays silent on
/// every success path. Keeps the LRO wiring and the silent-success contract from regressing without
/// a live subscription.
/// </summary>
public class RemoveResourceGroupDeploymentProcessorTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";
    private const string LocationUri = "https://management.azure.com/subscriptions/" + TestSubscriptionId + "/operationresults/opid?api-version=2025-04-01";

    private static RemoveResourceGroupDeploymentProcessor CreateProcessor(
        FakeHttpPipeline pipeline,
        FakeOutputHandler output)
    {
        // No real delay between polls; the poll loop is exercised, not the wait.
        pipeline.PollingInterval = 0;
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var processor = new RemoveResourceGroupDeploymentProcessor();
        processor.Init(new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile));
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroupName = "my-rg";
        processor.Name = "dep1";
        return processor;
    }

    private static HttpResponseMessage NoContent() => new(HttpStatusCode.NoContent);

    private static HttpResponseMessage Accepted(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Accepted);
        response.Headers.Add("Location", location);
        return response;
    }

    [Fact]
    public async Task ProcessAsync_DeletesDeploymentUriWithEscapedSegments()
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
        RemoveResourceGroupDeploymentProcessor processor = CreateProcessor(pipeline, output);
        processor.ResourceGroupName = "my rg";
        processor.Name = "dep#1";

        await processor.ProcessAsync();

        Assert.Equal(HttpMethod.Delete, sentMethod);
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourcegroups/my%20rg"
                + "/providers/Microsoft.Resources/deployments/dep%231?api-version=2025-04-01",
            sentUri!.AbsoluteUri);
    }

    // An absent deployment is answered 204 straight away: no polling, no final GET, no output.
    [Fact]
    public async Task ProcessAsync_ImmediateNoContentSendsOneRequestAndWritesNoOutput()
    {
        var pipeline = new FakeHttpPipeline((_, _) => NoContent());
        var output = new FakeOutputHandler();
        RemoveResourceGroupDeploymentProcessor processor = CreateProcessor(pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        Assert.Empty(output.WrittenValues);
    }

    // The documented happy path: 202 + Location, the Location URI answers 202 once more, then 204.
    // The last request must be the Location poll -- not a GET on the deleted deployment, which ARM
    // would answer 404 and turn a successful delete into a failure.
    [Fact]
    public async Task ProcessAsync_PollsLocationUntilNoContentAndNeverGetsTheDeployment()
    {
        var requests = new List<(HttpMethod Method, string Uri)>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!.AbsoluteUri));
            return requests.Count switch
            {
                1 => Accepted(LocationUri),
                2 => Accepted(LocationUri),
                _ => NoContent(),
            };
        });
        var output = new FakeOutputHandler();
        RemoveResourceGroupDeploymentProcessor processor = CreateProcessor(pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(3, requests.Count);
        Assert.Equal(HttpMethod.Delete, requests[0].Method);
        Assert.All(requests.Skip(1), r => Assert.Equal(HttpMethod.Get, r.Method));
        Assert.All(requests.Skip(1), r => Assert.Equal(LocationUri, r.Uri));
        Assert.Empty(output.WrittenValues);
    }

    // A terminal poll that answers 200 with a status document is still a successful delete, and the
    // command documents that it emits nothing -- the status document must not be written out.
    [Fact]
    public async Task ProcessAsync_TerminalStatusDocumentWritesNoOutput()
    {
        const string statusBody = """{"status":"Succeeded"}""";
        var count = 0;
        var pipeline = new FakeHttpPipeline((_, _) =>
        {
            count++;
            return count == 1
                ? Accepted(LocationUri)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(statusBody, Encoding.UTF8, "application/json")
                };
        });
        var output = new FakeOutputHandler();
        RemoveResourceGroupDeploymentProcessor processor = CreateProcessor(pipeline, output);

        await processor.ProcessAsync();

        Assert.Empty(output.WrittenValues);
    }

    // A deployment that is still running is rejected with 409, and that rejection must surface
    // rather than being swallowed by the long-running-operation handling.
    [Fact]
    public async Task ProcessAsync_RunningDeploymentConflictThrowsAndWritesNoOutput()
    {
        const string body = """
            {
              "error": {
                "code": "DeploymentCannotBeDeleted",
                "message": "Deployment 'dep1' cannot be deleted in the 'Running' state."
              }
            }
            """;
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
        var output = new FakeOutputHandler();
        RemoveResourceGroupDeploymentProcessor processor = CreateProcessor(pipeline, output);

        HttpRequestException failure = await Assert.ThrowsAsync<HttpRequestException>(processor.ProcessAsync);

        Assert.Equal(HttpStatusCode.Conflict, failure.StatusCode);
        Assert.Contains("DeploymentCannotBeDeleted", failure.Message);
        Assert.Equal(1, pipeline.SendCount);
        Assert.Empty(output.WrittenValues);
    }
}
