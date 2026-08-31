// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;
using Moq;

namespace Extensions.MsGraph.Tests;

/// <summary>
/// Tests the customization layer <see cref="ListGroupProcessor_Orchestrator"/> adds on top of the
/// atomic <c>ListGroupProcessor</c>: convenience filter shortcuts (DisplayName,
/// DisplayNamePrefix), the <c>AppendSelected</c> defaults merge, OData escaping, and combining
/// caller-supplied Filter with the shortcuts using OData <c>and</c>.
/// </summary>
public class ListGroupProcessor_OrchestratorTests
{
    private static IEventPublisher NoOpPublisher() => Mock.Of<IEventPublisher>();

    private static (ListGroupProcessor_Orchestrator orchestrator, FakeGraphHttpPipeline pipeline)
        Create(ListGroupProcessor_Orchestrator orch)
    {
        var pipeline = new FakeGraphHttpPipeline(
            _ => FakeGraphHttpPipeline.JsonResponse("{\"value\":[]}"));
        orch.WithFakeContext(pipeline, NoOpPublisher());
        return (orch, pipeline);
    }

    [Fact]
    public async Task SendAsync_NoInputs_ShouldTargetGroupsEndpointWithNoQuery()
    {
        var (orch, pipeline) = Create(new ListGroupProcessor_Orchestrator());

        using var _ = await orch.InvokeSendAsync();

        var sent = Assert.Single(pipeline.Sent);
        Assert.Equal(HttpMethod.Get, sent.Method);
        Assert.Equal("https://graph.microsoft.com/v1.0/groups", sent.RequestUri!.ToString());
    }

    [Fact]
    public async Task SendAsync_DisplayName_ShouldEmitEqualsFilter()
    {
        var (orch, pipeline) = Create(new ListGroupProcessor_Orchestrator { DisplayName = "Sales" });

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("displayName eq 'Sales'"), uri);
    }

    [Fact]
    public async Task SendAsync_DisplayNamePrefix_ShouldEmitStartswithFilter()
    {
        var (orch, pipeline) = Create(new ListGroupProcessor_Orchestrator { DisplayNamePrefix = "Sal" });

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("startswith(displayName,'Sal')"), uri);
    }

    [Fact]
    public async Task SendAsync_FilterAndShortcuts_ShouldCombineWithAnd()
    {
        var (orch, pipeline) = Create(new ListGroupProcessor_Orchestrator
        {
            Filter = "securityEnabled eq true",
            DisplayNamePrefix = "Sal",
        });

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        var expected =
            "securityEnabled eq true and startswith(displayName,'Sal')";
        Assert.Contains(Uri.EscapeDataString(expected), uri);
    }

    [Fact]
    public async Task SendAsync_ShouldEscapeSingleQuotesInDisplayName()
    {
        var (orch, pipeline) = Create(new ListGroupProcessor_Orchestrator { DisplayName = "O'Brien" });

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("displayName eq 'O''Brien'"), uri);
    }

    [Fact]
    public async Task SendAsync_AppendSelected_ShouldMergeDefaultsWithCallerSelect()
    {
        var (orch, pipeline) = Create(new ListGroupProcessor_Orchestrator
        {
            Select = new[] { "mailNickname" },
            AppendSelected = true,
        });

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.ToString();
        Assert.Contains("displayName", uri);
        Assert.Contains("mailNickname", uri);
    }

    [Fact]
    public async Task SendAsync_SelectOnly_ShouldPassThrough()
    {
        var (orch, pipeline) = Create(new ListGroupProcessor_Orchestrator
        {
            Select = new[] { "id", "displayName" },
        });

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.ToString();
        Assert.Contains("$select=id%2CdisplayName", uri);
    }

    [Fact]
    public async Task SendAsync_ConsistencyLevel_ShouldFlowIntoAtomicHeader()
    {
        var (orch, pipeline) = Create(new ListGroupProcessor_Orchestrator
        {
            Search = "\"displayName:contoso\"",
            ConsistencyLevel = "eventual",
        });

        using var _ = await orch.InvokeSendAsync();

        var sent = pipeline.Sent.Single();
        Assert.True(sent.Headers.TryGetValues("ConsistencyLevel", out var values));
        Assert.Equal("eventual", values!.Single());
    }
}
