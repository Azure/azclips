// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;
using Moq;

namespace Extensions.MsGraph.Tests;

/// <summary>
/// Tests the customization layer <see cref="GetUserProcessor_Orchestrator"/> adds on top of the
/// atomic <c>GetUserProcessor</c>: <c>AppendSelected</c> defaults merging, and passthrough of Id,
/// Select, and Expand into a single outgoing <c>GET /users/{id}</c> request.
/// </summary>
public class GetUserProcessor_OrchestratorTests
{
    private const string TestUserId = "48d31887-5fad-4d73-a9f5-3c356e68a038";

    private static IEventPublisher NoOpPublisher() => Mock.Of<IEventPublisher>();

    private static (GetUserProcessor_Orchestrator orchestrator, FakeGraphHttpPipeline pipeline)
        Create(string id, string[]? select = null, string[]? expand = null, bool appendSelected = false)
    {
        var pipeline = new FakeGraphHttpPipeline(
            _ => FakeGraphHttpPipeline.JsonResponse("{\"id\":\"" + TestUserId + "\"}"));
        var orch = new GetUserProcessor_Orchestrator
        {
            Id = id,
            Select = select,
            Expand = expand,
            AppendSelected = appendSelected,
        }.WithFakeContext(pipeline, NoOpPublisher());
        return (orch, pipeline);
    }

    [Fact]
    public async Task SendAsync_PassesIdToAtomic()
    {
        var (orch, pipeline) = Create(TestUserId);

        using var _ = await orch.InvokeSendAsync();

        var sent = Assert.Single(pipeline.Sent);
        Assert.Equal(HttpMethod.Get, sent.Method);
        Assert.Equal($"https://graph.microsoft.com/v1.0/users/{TestUserId}", sent.RequestUri!.ToString());
    }

    [Fact]
    public async Task SendAsync_SelectOnly_ShouldPassThroughVerbatim()
    {
        var (orch, pipeline) = Create(TestUserId, select: new[] { "id", "displayName" });

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.ToString();
        Assert.Contains("$select=id%2CdisplayName", uri);
    }

    [Fact]
    public async Task SendAsync_AppendSelected_ShouldMergeDefaultsWithCallerSelect()
    {
        var (orch, pipeline) = Create(TestUserId, select: new[] { "jobTitle" }, appendSelected: true);

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.ToString();
        Assert.Contains("displayName", uri);
        Assert.Contains("userPrincipalName", uri);
        Assert.Contains("jobTitle", uri);
    }

    [Fact]
    public async Task SendAsync_AppendSelectedWithoutCallerSelect_ShouldEmitDefaults()
    {
        var (orch, pipeline) = Create(TestUserId, appendSelected: true);

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.ToString();
        Assert.Contains("$select=", uri);
        Assert.Contains("displayName", uri);
    }

    [Fact]
    public async Task SendAsync_NoSelectAndAppendSelectedFalse_ShouldOmitSelect()
    {
        var (orch, pipeline) = Create(TestUserId);

        using var _ = await orch.InvokeSendAsync();

        Assert.DoesNotContain("$select", pipeline.Sent.Single().RequestUri!.ToString());
    }

    [Fact]
    public async Task SendAsync_ExpandOnly_ShouldPassThrough()
    {
        var (orch, pipeline) = Create(TestUserId, expand: new[] { "memberOf" });

        using var _ = await orch.InvokeSendAsync();

        Assert.EndsWith("?$expand=memberOf", pipeline.Sent.Single().RequestUri!.ToString());
    }
}
