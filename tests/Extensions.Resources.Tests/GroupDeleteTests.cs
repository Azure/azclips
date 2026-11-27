// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Tests.Common.Stubs;

namespace Extensions.Resources.Tests;

public class GroupDeleteTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    [Fact]
    public async Task ProcessAsync_DeletesByNameWithoutOptionalQueryOrOutput()
    {
        HttpRequestMessage? sentRequest = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentRequest = request;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var output = new FakeOutputHandler();
        DeleteResourceGroupProcessor_Orchestrator processor = CreateProcessor(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroupName = "group with spaces";

        await processor.ProcessAsync();

        Assert.Equal(HttpMethod.Delete, sentRequest!.Method);
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourcegroups/group%20with%20spaces?api-version=2025-04-01",
            sentRequest.RequestUri!.AbsoluteUri);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task ProcessAsync_ResolvesResourceGroupIdAndEncodesForceDeletionTypes()
    {
        Uri? sentUri = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        DeleteResourceGroupProcessor_Orchestrator processor = CreateProcessor(
            pipeline,
            new FakeOutputHandler());
        processor.Id = "/subscriptions/id-subscription/resourceGroups/test-rg";
        processor.ForceDeletionTypes = "Microsoft.Compute/virtualMachines";

        await processor.ProcessAsync();

        Assert.Equal(
            "https://management.azure.com/subscriptions/id-subscription/resourcegroups/test-rg?api-version=2025-04-01&forceDeletionTypes=Microsoft.Compute%2FvirtualMachines",
            sentUri!.AbsoluteUri);
        Assert.Equal("id-subscription", processor.SubscriptionId);
        Assert.Equal("test-rg", processor.ResourceGroupName);
    }

    [Fact]
    public async Task ProcessAsync_PollsAsyncOperationWithoutFinalGet()
    {
        var pollingUri = new Uri("https://management.azure.com/operations/delete-1");
        var requests = new List<(HttpMethod Method, Uri? Uri)>();
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri));
            if (requests.Count == 1)
            {
                var accepted = new HttpResponseMessage(HttpStatusCode.Accepted);
                accepted.Headers.TryAddWithoutValidation("Azure-AsyncOperation", pollingUri.AbsoluteUri);
                return accepted;
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"status":"Succeeded"}""", Encoding.UTF8, "application/json"),
            };
        });
        DeleteResourceGroupProcessor_Orchestrator processor = CreateProcessor(
            pipeline,
            new FakeOutputHandler());
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroupName = "test-rg";

        await processor.ProcessAsync();

        Assert.Equal(2, pipeline.SendCount);
        Assert.Equal(HttpMethod.Delete, requests[0].Method);
        Assert.Equal((HttpMethod.Get, pollingUri), requests[1]);
    }

    [Fact]
    public async Task ProcessAsync_RejectsInvalidOrAmbiguousResourceGroupAddress()
    {
        DeleteResourceGroupProcessor_Orchestrator processor = CreateProcessor(
            new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.NoContent)),
            new FakeOutputHandler());

        InvalidInputException missing = await Assert.ThrowsAsync<InvalidInputException>(
            () => processor.ProcessAsync());
        Assert.Contains("exactly one", missing.Message);

        processor.ResourceGroupName = "test-rg";
        processor.Id = "/subscriptions/sub/resourceGroups/test-rg";
        InvalidInputException ambiguous = await Assert.ThrowsAsync<InvalidInputException>(
            () => processor.ProcessAsync());
        Assert.Contains("exactly one", ambiguous.Message);

        processor.ResourceGroupName = string.Empty;
        processor.Id = "/subscriptions/sub/providers/Microsoft.Resources/resourceGroups/test-rg";
        InvalidInputException invalidId = await Assert.ThrowsAsync<InvalidInputException>(
            () => processor.ProcessAsync());
        Assert.Contains("Invalid resource group id", invalidId.Message);
    }

    [Fact]
    public async Task ProcessAsync_PropagatesServiceFailure()
    {
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        DeleteResourceGroupProcessor_Orchestrator processor = CreateProcessor(
            pipeline,
            new FakeOutputHandler());
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroupName = "test-rg";

        HttpRequestException exception =
            await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
    }

    private static DeleteResourceGroupProcessor_Orchestrator CreateProcessor(
        FakeHttpPipeline pipeline,
        FakeOutputHandler output)
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var processor = new DeleteResourceGroupProcessor_Orchestrator();
        processor.Init(new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile));
        return processor;
    }
}
