// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;
using Moq;

namespace Extensions.MsGraph.Tests;

/// <summary>
/// Tests the customization layer <see cref="CheckGroupMemberProcessor_Orchestrator"/> adds on top
/// of the atomic <c>CheckGroupMemberProcessor</c>: display-name-prefix resolution to a single
/// object id, and required-field validation.
/// </summary>
public class CheckGroupMemberProcessor_OrchestratorTests
{
    private const string GroupId = "11111111-2222-3333-4444-555555555555";
    private const string MemberId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

    private static IEventPublisher NoOpPublisher() => Mock.Of<IEventPublisher>();

    [Fact]
    public async Task SendAsync_WithGuid_ShouldSkipResolutionAndPost()
    {
        var pipeline = new FakeGraphHttpPipeline(
            request => FakeGraphHttpPipeline.JsonResponse("{\"value\":[\"" + GroupId + "\"]}"));
        var orch = new CheckGroupMemberProcessor_Orchestrator
        {
            Group = GroupId,
            MemberId = MemberId,
        }.WithFakeContext(pipeline, NoOpPublisher());

        using var _ = await orch.InvokeSendAsync();

        var sent = Assert.Single(pipeline.Sent);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal(
            $"https://graph.microsoft.com/v1.0/directoryObjects/{MemberId}/checkMemberGroups",
            sent.RequestUri!.ToString());
    }

    [Fact]
    public async Task SendAsync_WithDisplayNamePrefix_ShouldResolveThenPost()
    {
        var listBody = $"{{\"value\":[{{\"id\":\"{GroupId}\",\"displayName\":\"Sales\"}}]}}";
        var pipeline = new FakeGraphHttpPipeline(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/v1.0/groups", StringComparison.Ordinal))
            {
                return FakeGraphHttpPipeline.JsonResponse(listBody);
            }
            return FakeGraphHttpPipeline.JsonResponse("{\"value\":[\"" + GroupId + "\"]}");
        });

        var orch = new CheckGroupMemberProcessor_Orchestrator
        {
            Group = "Sales",
            MemberId = MemberId,
        }.WithFakeContext(pipeline, NoOpPublisher());

        using var _ = await orch.InvokeSendAsync();

        Assert.Equal(2, pipeline.Sent.Count);
        Assert.Contains(Uri.EscapeDataString("startswith(displayName,'Sales')"), pipeline.Sent[0].RequestUri!.OriginalString);
        Assert.Equal(HttpMethod.Post, pipeline.Sent[1].Method);
        Assert.EndsWith($"/directoryObjects/{MemberId}/checkMemberGroups", pipeline.Sent[1].RequestUri!.AbsolutePath);
    }

    // Fails the test with a different exception if the orchestrator sends any request.
    private static FakeGraphHttpPipeline NoRequestPipeline() =>
        new(_ => throw new InvalidOperationException("Unexpected request."));

    // Answers only the group lookup; the membership check request fails the test.
    private static FakeGraphHttpPipeline LookupOnlyPipeline(string listResponseBody) =>
        new(request => request.RequestUri!.AbsolutePath.EndsWith("/v1.0/groups", StringComparison.Ordinal)
            ? FakeGraphHttpPipeline.JsonResponse(listResponseBody)
            : throw new InvalidOperationException("Unexpected request."));

    [Fact]
    public async Task SendAsync_MissingGroup_ThrowsBeforeHttp()
    {
        var orch = new CheckGroupMemberProcessor_Orchestrator
        {
            Group = "",
            MemberId = MemberId,
        }.WithFakeContext(NoRequestPipeline(), NoOpPublisher());

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => orch.InvokeSendAsync());

        Assert.Equal("Group is required.", ex.Message);
    }

    [Fact]
    public async Task SendAsync_MissingMemberId_ThrowsBeforeHttp()
    {
        var orch = new CheckGroupMemberProcessor_Orchestrator
        {
            Group = GroupId,
            MemberId = "",
        }.WithFakeContext(NoRequestPipeline(), NoOpPublisher());

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => orch.InvokeSendAsync());

        Assert.Equal("MemberId is required to check group membership.", ex.Message);
    }

    [Fact]
    public async Task SendAsync_ResolutionReturnsNoMatches_ThrowsNotFound()
    {
        var orch = new CheckGroupMemberProcessor_Orchestrator
        {
            Group = "Nope",
            MemberId = MemberId,
        }.WithFakeContext(LookupOnlyPipeline("{\"value\":[]}"), NoOpPublisher());

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => orch.InvokeSendAsync());

        Assert.Equal("Group 'Nope' is not found in Graph.", ex.Message);
    }

    [Fact]
    public async Task SendAsync_ResolutionReturnsMultipleMatches_ThrowsAmbiguous()
    {
        var orch = new CheckGroupMemberProcessor_Orchestrator
        {
            Group = "Sales",
            MemberId = MemberId,
        }.WithFakeContext(LookupOnlyPipeline(
            "{\"value\":[" +
            "{\"id\":\"11111111-2222-3333-4444-555555555555\",\"displayName\":\"Sales-North\"}," +
            "{\"id\":\"66666666-7777-8888-9999-aaaaaaaaaaaa\",\"displayName\":\"Sales-South\"}]}"), NoOpPublisher());

        var ex = await Assert.ThrowsAsync<InvalidInputException>(() => orch.InvokeSendAsync());

        Assert.Equal("More than one group matched 'Sales'. Use the object id instead.", ex.Message);
    }
}
