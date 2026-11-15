// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;
using Moq;

namespace Extensions.MsGraph.Tests;

/// <summary>
/// Tests the customization layer <see cref="GetGroupProcessor_Orchestrator"/> adds on top of the
/// atomic <c>GetGroupProcessor</c>: display-name-prefix resolution to a single object id, the
/// <c>AppendSelected</c> defaults merging, and deterministic errors when zero/many groups match.
/// </summary>
public class GetGroupProcessor_OrchestratorTests
{
    private const string GroupId = "11111111-2222-3333-4444-555555555555";

    private static IEventPublisher NoOpPublisher() => Mock.Of<IEventPublisher>();

    private static (GetGroupProcessor_Orchestrator orchestrator, FakeGraphHttpPipeline pipeline)
        CreateWithResolution(
            string groupIdentifier,
            string listResponseBody,
            bool appendSelected = false,
            string[]? select = null,
            string[]? expand = null)
    {
        var pipeline = new FakeGraphHttpPipeline(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            // Groups list (for resolution): "/v1.0/groups" (no trailing id)
            if (path.EndsWith("/v1.0/groups", StringComparison.Ordinal))
            {
                return FakeGraphHttpPipeline.JsonResponse(listResponseBody);
            }
            return FakeGraphHttpPipeline.JsonResponse("{\"id\":\"" + GroupId + "\"}");
        });
        var orch = new GetGroupProcessor_Orchestrator
        {
            Group = groupIdentifier,
            Select = select,
            Expand = expand,
            AppendSelected = appendSelected,
        }.WithFakeContext(pipeline, NoOpPublisher());
        return (orch, pipeline);
    }

    [Fact]
    public async Task SendAsync_WithGuid_ShouldSkipResolutionAndTargetGroupsEndpoint()
    {
        var pipeline = new FakeGraphHttpPipeline(
            _ => FakeGraphHttpPipeline.JsonResponse("{\"id\":\"" + GroupId + "\"}"));
        var orch = new GetGroupProcessor_Orchestrator { Group = GroupId }
            .WithFakeContext(pipeline, NoOpPublisher());

        using var _ = await orch.InvokeSendAsync();

        var sent = Assert.Single(pipeline.Sent);
        Assert.Equal(HttpMethod.Get, sent.Method);
        Assert.Equal($"https://graph.microsoft.com/v1.0/groups/{GroupId}", sent.RequestUri!.ToString());
    }

    [Fact]
    public async Task SendAsync_WithDisplayNamePrefix_ShouldIssueResolutionThenGet()
    {
        var listBody = $"{{\"value\":[{{\"id\":\"{GroupId}\",\"displayName\":\"Sales\"}}]}}";
        var (orch, pipeline) = CreateWithResolution("Sales", listBody);

        using var _ = await orch.InvokeSendAsync();

        Assert.Equal(2, pipeline.Sent.Count);
        // First: list with startswith filter on displayName
        var listUri = pipeline.Sent[0].RequestUri!.OriginalString;
        Assert.Contains("/groups?", listUri);
        Assert.Contains(Uri.EscapeDataString("startswith(displayName,'Sales')"), listUri);
        // Second: GET /groups/{resolvedId}
        Assert.Equal(
            $"https://graph.microsoft.com/v1.0/groups/{GroupId}",
            pipeline.Sent[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task SendAsync_WithDisplayNameContainingSingleQuote_ShouldEscape()
    {
        var listBody = $"{{\"value\":[{{\"id\":\"{GroupId}\",\"displayName\":\"O'Brien\"}}]}}";
        var (orch, pipeline) = CreateWithResolution("O'Brien", listBody);

        using var _ = await orch.InvokeSendAsync();

        // Doubled single quote: O''Brien inside the startswith clause.
        var listUri = pipeline.Sent[0].RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("startswith(displayName,'O''Brien')"), listUri);
    }

    [Fact]
    public async Task SendAsync_ResolutionReturnsNoMatches_ShouldThrow()
    {
        var (orch, _) = CreateWithResolution("Ghost", "{\"value\":[]}");

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => orch.InvokeSendAsync());
        Assert.Equal("Group 'Ghost' is not found in Graph.", ex.Message);
    }

    [Fact]
    public async Task SendAsync_ResolutionReturnsMultipleMatches_ShouldThrow()
    {
        var listBody =
            "{\"value\":[" +
            "{\"id\":\"11111111-2222-3333-4444-555555555555\",\"displayName\":\"Sales-North\"}," +
            "{\"id\":\"66666666-7777-8888-9999-aaaaaaaaaaaa\",\"displayName\":\"Sales-South\"}" +
            "]}";
        var (orch, _) = CreateWithResolution("Sales", listBody);

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => orch.InvokeSendAsync());
        Assert.Equal("More than one group matched 'Sales'. Use the object id instead.", ex.Message);
    }

    [Fact]
    public async Task SendAsync_MissingGroup_ThrowsBeforeHttp()
    {
        var pipeline = new FakeGraphHttpPipeline(_ => throw new InvalidOperationException("Unexpected request."));
        var orch = new GetGroupProcessor_Orchestrator { Group = "" }
            .WithFakeContext(pipeline, NoOpPublisher());

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => orch.InvokeSendAsync());

        Assert.Equal("Group is required.", ex.Message);
    }

    [Fact]
    public async Task SendAsync_AppendSelected_ShouldMergeDefaultsOnFinalRequest()
    {
        var pipeline = new FakeGraphHttpPipeline(
            _ => FakeGraphHttpPipeline.JsonResponse("{\"id\":\"" + GroupId + "\"}"));
        var orch = new GetGroupProcessor_Orchestrator
        {
            Group = GroupId,
            Select = new[] { "mailNickname" },
            AppendSelected = true,
        }.WithFakeContext(pipeline, NoOpPublisher());

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.ToString();
        Assert.Contains("displayName", uri);
        Assert.Contains("mailNickname", uri);
    }
}
