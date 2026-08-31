// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;
using Moq;

namespace Extensions.MsGraph.Tests;

/// <summary>
/// Tests the customization layer <see cref="ListGroupMemberProcessor_Orchestrator"/> adds on top of
/// the atomic <c>ListGroupMemberProcessor</c>: display-name-prefix and exact-display-name
/// resolution to a single group object id, and required-field validation.
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
    public async Task SendAsync_WithGuidGroup_ShouldSkipResolutionAndTargetMembers()
    {
        var pipeline = new FakeGraphHttpPipeline(_ => FakeGraphHttpPipeline.JsonResponse("{\"value\":[]}"));
        var orch = new ListGroupMemberProcessor_Orchestrator { Group = GroupId }
            .WithFakeContext(pipeline, NoOpPublisher());

        using var _ = await orch.InvokeSendAsync();

        var sent = Assert.Single(pipeline.Sent);
        Assert.Equal(HttpMethod.Get, sent.Method);
        Assert.Equal($"https://graph.microsoft.com/v1.0/groups/{GroupId}/members", sent.RequestUri!.ToString());
    }

    [Fact]
    public async Task SendAsync_WithGroupPrefix_ShouldUseStartswithFilter()
    {
        var pipeline = PipelineForResolution(
            $"{{\"value\":[{{\"id\":\"{GroupId}\",\"displayName\":\"Sales\"}}]}}");
        var orch = new ListGroupMemberProcessor_Orchestrator { Group = "Sales" }
            .WithFakeContext(pipeline, NoOpPublisher());

        using var _ = await orch.InvokeSendAsync();

        Assert.Equal(2, pipeline.Sent.Count);
        Assert.Contains(Uri.EscapeDataString("startswith(displayName,'Sales')"), pipeline.Sent[0].RequestUri!.OriginalString);
        Assert.Equal(
            $"https://graph.microsoft.com/v1.0/groups/{GroupId}/members",
            pipeline.Sent[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task SendAsync_WithGroupDisplayName_ShouldUseEqualsFilter()
    {
        var pipeline = PipelineForResolution(
            $"{{\"value\":[{{\"id\":\"{GroupId}\",\"displayName\":\"Sales\"}}]}}");
        var orch = new ListGroupMemberProcessor_Orchestrator { GroupDisplayName = "Sales" }
            .WithFakeContext(pipeline, NoOpPublisher());

        using var _ = await orch.InvokeSendAsync();

        Assert.Equal(2, pipeline.Sent.Count);
        // Uses eq (not startswith) for the resolution step.
        var listUri = pipeline.Sent[0].RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("displayName eq 'Sales'"), listUri);
        Assert.DoesNotContain("startswith", listUri);
    }

    [Fact]
    public async Task SendAsync_WithGroupDisplayNameContainingSingleQuote_ShouldEscape()
    {
        var pipeline = PipelineForResolution(
            $"{{\"value\":[{{\"id\":\"{GroupId}\",\"displayName\":\"O'Brien\"}}]}}");
        var orch = new ListGroupMemberProcessor_Orchestrator { GroupDisplayName = "O'Brien" }
            .WithFakeContext(pipeline, NoOpPublisher());

        using var _ = await orch.InvokeSendAsync();

        var listUri = pipeline.Sent[0].RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("displayName eq 'O''Brien'"), listUri);
    }

    [Fact]
    public async Task SendAsync_MissingBothIdentifiers_ShouldThrowBeforeHttp()
    {
        var pipeline = new FakeGraphHttpPipeline(_ => FakeGraphHttpPipeline.JsonResponse("{}"));
        var orch = new ListGroupMemberProcessor_Orchestrator()
            .WithFakeContext(pipeline, NoOpPublisher());

        await Assert.ThrowsAsync<InvalidOperationException>(() => orch.InvokeSendAsync());
        Assert.Empty(pipeline.Sent);
    }

    [Fact]
    public async Task SendAsync_GroupDisplayNameWins_WhenBothProvided()
    {
        var pipeline = PipelineForResolution(
            $"{{\"value\":[{{\"id\":\"{GroupId}\",\"displayName\":\"Sales\"}}]}}");
        var orch = new ListGroupMemberProcessor_Orchestrator
        {
            Group = "Marketing",
            GroupDisplayName = "Sales",
        }.WithFakeContext(pipeline, NoOpPublisher());

        using var _ = await orch.InvokeSendAsync();

        // The exact-name path should be used, so the list filter uses eq (not startswith).
        var listUri = pipeline.Sent[0].RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("displayName eq 'Sales'"), listUri);
        Assert.DoesNotContain("startswith", listUri);
        Assert.DoesNotContain("Marketing", listUri);
    }
}
