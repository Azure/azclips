// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;
using Moq;

namespace Extensions.MsGraph.Tests;

/// <summary>
/// Tests the customization layer <see cref="GetSignedInUserProcessor_Orchestrator"/> adds on top of
/// the atomic <c>GetSignedInUserProcessor</c>: <c>AppendSelected</c> defaults merging for
/// <c>GET /me</c>.
/// </summary>
public class GetSignedInUserProcessor_OrchestratorTests
{
    private static IEventPublisher NoOpPublisher() => Mock.Of<IEventPublisher>();

    private static (GetSignedInUserProcessor_Orchestrator orchestrator, FakeGraphHttpPipeline pipeline)
        Create(string[]? select = null, string[]? expand = null, bool appendSelected = false)
    {
        var pipeline = new FakeGraphHttpPipeline(
            _ => FakeGraphHttpPipeline.JsonResponse("{}"));
        var orch = new GetSignedInUserProcessor_Orchestrator
        {
            Select = select,
            Expand = expand,
            AppendSelected = appendSelected,
        }.WithFakeContext(pipeline, NoOpPublisher());
        return (orch, pipeline);
    }

    [Fact]
    public async Task SendAsync_ShouldTargetMeEndpoint()
    {
        var (orch, pipeline) = Create();

        using var _ = await orch.InvokeSendAsync();

        var sent = Assert.Single(pipeline.Sent);
        Assert.Equal(HttpMethod.Get, sent.Method);
        Assert.Equal("https://graph.microsoft.com/v1.0/me", sent.RequestUri!.ToString());
    }

    [Fact]
    public async Task SendAsync_AppendSelected_ShouldMergeDefaultsWithCallerSelect()
    {
        var (orch, pipeline) = Create(select: new[] { "jobTitle" }, appendSelected: true);

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.ToString();
        Assert.Contains("displayName", uri);
        Assert.Contains("userPrincipalName", uri);
        Assert.Contains("jobTitle", uri);
    }

    [Fact]
    public async Task SendAsync_SelectOnly_ShouldPassThrough()
    {
        var (orch, pipeline) = Create(select: new[] { "id", "displayName" });

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.ToString();
        Assert.Contains("$select=id%2CdisplayName", uri);
    }

    [Fact]
    public async Task SendAsync_ExpandOnly_ShouldPassThrough()
    {
        var (orch, pipeline) = Create(expand: new[] { "memberOf" });

        using var _ = await orch.InvokeSendAsync();

        Assert.EndsWith("?$expand=memberOf", pipeline.Sent.Single().RequestUri!.ToString());
    }
}
