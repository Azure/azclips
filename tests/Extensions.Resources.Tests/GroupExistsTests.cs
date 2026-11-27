// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Tests.Common.Stubs;

namespace Extensions.Resources.Tests;

public class GroupExistsTests
{
    private const string TestSubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    [Theory]
    [InlineData(HttpStatusCode.NoContent, true)]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.NotFound, false)]
    public async Task ProcessAsync_UsesHeadAndWritesExistenceResult(HttpStatusCode statusCode, bool expected)
    {
        HttpMethod? sentMethod = null;
        Uri? sentUri = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            sentMethod = request.Method;
            sentUri = request.RequestUri;
            return new HttpResponseMessage(statusCode);
        });
        var output = new FakeOutputHandler();
        ExistsResourceGroupProcessor_Orchestrator processor = CreateProcessor(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroupName = "test-rg";

        await processor.ProcessAsync();

        Assert.Equal(HttpMethod.Head, sentMethod);
        Assert.Equal(
            $"https://management.azure.com/subscriptions/{TestSubscriptionId}/resourcegroups/test-rg?api-version=2025-04-01",
            sentUri!.ToString());
        Assert.Equal(expected, Assert.IsType<bool>(Assert.Single(output.WrittenValues)));
    }

    [Fact]
    public async Task ProcessAsync_ThrowsForUnexpectedFailure()
    {
        var pipeline = new FakeHttpPipeline((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            ReasonPhrase = "Internal Server Error",
        });
        var output = new FakeOutputHandler();
        ExistsResourceGroupProcessor_Orchestrator processor = CreateProcessor(pipeline, output);
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroupName = "test-rg";

        HttpRequestException exception =
            await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Empty(output.WrittenValues);
    }

    private static ExistsResourceGroupProcessor_Orchestrator CreateProcessor(
        FakeHttpPipeline pipeline,
        FakeOutputHandler output)
    {
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = TestSubscriptionId };
        var processor = new ExistsResourceGroupProcessor_Orchestrator();
        processor.Init(new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile));
        return processor;
    }
}
