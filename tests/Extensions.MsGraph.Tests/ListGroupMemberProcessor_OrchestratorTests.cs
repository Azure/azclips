// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;
using Moq;

namespace Extensions.MsGraph.Tests;

/// <summary>
/// Tests the customization layer <see cref="ListGroupMemberProcessor_Orchestrator"/> adds on top of
/// the atomic <c>ListGroupMemberProcessor</c>: display-name-prefix and exact-display-name
/// resolution to a single group object id, and required-field validation. Driven through
/// <c>ProcessAsync</c> (the real entry point) so the resolution and member requests are observed.
/// </summary>
public class ListGroupMemberProcessor_OrchestratorTests
{
    private const string GroupId = "11111111-2222-3333-4444-555555555555";

    private static IEventPublisher NoOpPublisher() => Mock.Of<IEventPublisher>();

    private static FakeGraphHttpPipeline PipelineForResolution(string listResponseBody) =>
        new(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/v1.0/groups", StringComparison.Ordinal))
            {
                return FakeGraphHttpPipeline.JsonResponse(listResponseBody);
            }
            return FakeGraphHttpPipeline.JsonResponse("{\"value\":[]}");
        });

    [Fact]
    public async Task ProcessAsync_WithGuidGroup_ShouldSkipResolutionAndTargetMembers()
    {
        var pipeline = new FakeGraphHttpPipeline(_ => FakeGraphHttpPipeline.JsonResponse("{\"value\":[]}"));
        var orch = new ListGroupMemberProcessor_Orchestrator { Group = GroupId };
        orch.WithCapturingContext(pipeline, NoOpPublisher());

        await orch.ProcessAsync();

        var sent = Assert.Single(pipeline.Sent);
        Assert.Equal(HttpMethod.Get, sent.Method);
        Assert.Equal($"https://graph.microsoft.com/v1.0/groups/{GroupId}/members", sent.RequestUri!.ToString());
    }

    [Fact]
    public async Task ProcessAsync_WithGroupPrefix_ShouldUseStartswithFilter()
    {
        var pipeline = PipelineForResolution(
            $"{{\"value\":[{{\"id\":\"{GroupId}\",\"displayName\":\"Sales\"}}]}}");
        var orch = new ListGroupMemberProcessor_Orchestrator { Group = "Sales" };
        orch.WithCapturingContext(pipeline, NoOpPublisher());

        await orch.ProcessAsync();

        Assert.Equal(2, pipeline.Sent.Count);
        Assert.Contains(Uri.EscapeDataString("startswith(displayName,'Sales')"), pipeline.Sent[0].RequestUri!.OriginalString);
        Assert.Equal(
            $"https://graph.microsoft.com/v1.0/groups/{GroupId}/members",
            pipeline.Sent[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task ProcessAsync_WithGroupDisplayName_ShouldUseEqualsFilter()
    {
        var pipeline = PipelineForResolution(
            $"{{\"value\":[{{\"id\":\"{GroupId}\",\"displayName\":\"Sales\"}}]}}");
        var orch = new ListGroupMemberProcessor_Orchestrator { GroupDisplayName = "Sales" };
        orch.WithCapturingContext(pipeline, NoOpPublisher());

        await orch.ProcessAsync();

        Assert.Equal(2, pipeline.Sent.Count);
        // Uses eq (not startswith) for the resolution step.
        var listUri = pipeline.Sent[0].RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("displayName eq 'Sales'"), listUri);
        Assert.DoesNotContain("startswith", listUri);
    }

    [Fact]
    public async Task ProcessAsync_WithGroupDisplayNameContainingSingleQuote_ShouldEscape()
    {
        var pipeline = PipelineForResolution(
            $"{{\"value\":[{{\"id\":\"{GroupId}\",\"displayName\":\"O'Brien\"}}]}}");
        var orch = new ListGroupMemberProcessor_Orchestrator { GroupDisplayName = "O'Brien" };
        orch.WithCapturingContext(pipeline, NoOpPublisher());

        await orch.ProcessAsync();

        var listUri = pipeline.Sent[0].RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("displayName eq 'O''Brien'"), listUri);
    }

    [Fact]
    public async Task ProcessAsync_MissingBothIdentifiers_ShouldThrowBeforeHttp()
    {
        var pipeline = new FakeGraphHttpPipeline(_ => FakeGraphHttpPipeline.JsonResponse("{}"));
        var orch = new ListGroupMemberProcessor_Orchestrator();
        orch.WithCapturingContext(pipeline, NoOpPublisher());

        await Assert.ThrowsAsync<InvalidInputException>(() => orch.ProcessAsync());
        Assert.Empty(pipeline.Sent);
    }

    [Fact]
    public async Task ProcessAsync_WhenBothIdentifiersProvided_ShouldThrowBeforeHttp()
    {
        var pipeline = new FakeGraphHttpPipeline(_ => FakeGraphHttpPipeline.JsonResponse("{}"));
        var orch = new ListGroupMemberProcessor_Orchestrator
        {
            Group = "Marketing",
            GroupDisplayName = "Sales",
        };
        orch.WithCapturingContext(pipeline, NoOpPublisher());

        await Assert.ThrowsAsync<InvalidInputException>(() => orch.ProcessAsync());
        Assert.Empty(pipeline.Sent);
    }

    [Fact]
    public async Task ProcessAsync_WhenResolutionFails_StillPublishesProcessorProcessAsyncEnd()
    {
        // Resolution runs inside the base ProcessAsync start/end lifecycle, so a failure must still
        // emit the paired end event rather than escaping the processor span.
        var publishedEvents = new List<string>();
        var publisher = new Mock<IEventPublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<string>()))
            .Callback<string>(publishedEvents.Add)
            .Returns(Task.CompletedTask);

        var pipeline = new FakeGraphHttpPipeline(_ => FakeGraphHttpPipeline.JsonResponse("{}"));
        var orch = new ListGroupMemberProcessor_Orchestrator();
        orch.WithCapturingContext(pipeline, publisher.Object);

        await Assert.ThrowsAsync<InvalidInputException>(() => orch.ProcessAsync());

        Assert.Contains(Events.ProcessorProcessAsyncEnd, publishedEvents);
        Assert.Empty(pipeline.Sent);
    }
}
