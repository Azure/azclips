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

    // Fails the test with a different exception if the orchestrator sends any request.
    private static FakeGraphHttpPipeline NoRequestPipeline() =>
        new(_ => throw new InvalidOperationException("Unexpected request."));

    // Answers only the group lookup; any later request (e.g. members) fails the test.
    private static FakeGraphHttpPipeline LookupOnlyPipeline(string listResponseBody) =>
        new(request => request.RequestUri!.AbsolutePath.EndsWith("/v1.0/groups", StringComparison.Ordinal)
            ? FakeGraphHttpPipeline.JsonResponse(listResponseBody)
            : throw new InvalidOperationException("Unexpected request."));

    [Fact]
    public async Task ProcessAsync_GroupDisplayNameNotFound_ThrowsNotFound()
    {
        var orch = new ListGroupMemberProcessor_Orchestrator { GroupDisplayName = "Ghost" };
        orch.WithCapturingContext(LookupOnlyPipeline("{\"value\":[]}"), NoOpPublisher());

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => orch.ProcessAsync());

        Assert.Equal("Group 'Ghost' is not found in Graph.", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_GroupPrefixMatchesMany_ThrowsAmbiguous()
    {
        var orch = new ListGroupMemberProcessor_Orchestrator { Group = "Sales" };
        orch.WithCapturingContext(LookupOnlyPipeline(
            "{\"value\":[" +
            "{\"id\":\"11111111-2222-3333-4444-555555555555\",\"displayName\":\"Sales-North\"}," +
            "{\"id\":\"66666666-7777-8888-9999-aaaaaaaaaaaa\",\"displayName\":\"Sales-South\"}]}"), NoOpPublisher());

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => orch.ProcessAsync());

        Assert.Equal("More than one group matched 'Sales'. Use the object id instead.", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_MissingBothIdentifiers_ThrowsBeforeHttp()
    {
        var orch = new ListGroupMemberProcessor_Orchestrator();
        orch.WithCapturingContext(NoRequestPipeline(), NoOpPublisher());

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => orch.ProcessAsync());

        Assert.Equal("A group identifier is required. Provide Group or GroupDisplayName.", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_WhenBothIdentifiersProvided_ThrowsBeforeHttp()
    {
        var orch = new ListGroupMemberProcessor_Orchestrator
        {
            Group = "Marketing",
            GroupDisplayName = "Sales",
        };
        orch.WithCapturingContext(NoRequestPipeline(), NoOpPublisher());

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => orch.ProcessAsync());

        Assert.Equal("Provide either Group or GroupDisplayName, but not both.", ex.Message);
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

        var orch = new ListGroupMemberProcessor_Orchestrator();
        orch.WithCapturingContext(NoRequestPipeline(), publisher.Object);

        await Assert.ThrowsAsync<InvalidInputException>(() => orch.ProcessAsync());

        Assert.Contains(Events.ProcessorProcessAsyncEnd, publishedEvents);
    }
}
