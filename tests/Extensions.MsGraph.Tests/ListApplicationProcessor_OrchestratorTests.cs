// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;
using Moq;

namespace Extensions.MsGraph.Tests;

/// <summary>
/// Tests the customization layer <see cref="ListApplicationProcessor_Orchestrator"/> adds on top of
/// the atomic <c>ListApplicationProcessor</c>: the <c>DisplayName</c> filter shortcut (translated
/// into an OData clause), OData escaping, and combining caller-supplied Filter with the shortcut
/// using OData <c>and</c>.
/// </summary>
public class ListApplicationProcessor_OrchestratorTests
{
    private static IEventPublisher NoOpPublisher() => Mock.Of<IEventPublisher>();

    private static (ListApplicationProcessor_Orchestrator orchestrator, FakeGraphHttpPipeline pipeline)
        Create(ListApplicationProcessor_Orchestrator orch)
    {
        var pipeline = new FakeGraphHttpPipeline(
            _ => FakeGraphHttpPipeline.JsonResponse("{\"value\":[]}"));
        orch.WithFakeContext(pipeline, NoOpPublisher());
        return (orch, pipeline);
    }

    [Fact]
    public async Task SendAsync_NoInputs_ShouldTargetApplicationsEndpointWithNoQuery()
    {
        var (orch, pipeline) = Create(new ListApplicationProcessor_Orchestrator());

        using var _ = await orch.InvokeSendAsync();

        var sent = Assert.Single(pipeline.Sent);
        Assert.Equal(HttpMethod.Get, sent.Method);
        Assert.Equal("https://graph.microsoft.com/v1.0/applications", sent.RequestUri!.ToString());
    }

    [Fact]
    public async Task SendAsync_DisplayName_ShouldEmitEqualsFilter()
    {
        var (orch, pipeline) = Create(new ListApplicationProcessor_Orchestrator { DisplayName = "contoso-app" });

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("displayName eq 'contoso-app'"), uri);
    }

    [Fact]
    public async Task SendAsync_FilterAndDisplayName_ShouldCombineWithAnd()
    {
        var (orch, pipeline) = Create(new ListApplicationProcessor_Orchestrator
        {
            Filter = "signInAudience eq 'AzureADMyOrg'",
            DisplayName = "contoso-app",
        });

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        var expected = "signInAudience eq 'AzureADMyOrg' and displayName eq 'contoso-app'";
        Assert.Contains(Uri.EscapeDataString(expected), uri);
    }

    [Fact]
    public async Task SendAsync_ShouldEscapeSingleQuotesInDisplayName()
    {
        var (orch, pipeline) = Create(new ListApplicationProcessor_Orchestrator { DisplayName = "o'brien" });

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("displayName eq 'o''brien'"), uri);
    }

    [Fact]
    public async Task SendAsync_SelectOnly_ShouldPassThrough()
    {
        var (orch, pipeline) = Create(new ListApplicationProcessor_Orchestrator
        {
            Select = new[] { "displayName", "appId" },
        });

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.ToString();
        Assert.Contains("$select=displayName%2CappId", uri);
    }

    [Fact]
    public async Task SendAsync_ConsistencyLevel_ShouldFlowIntoAtomicHeader()
    {
        var (orch, pipeline) = Create(new ListApplicationProcessor_Orchestrator
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
