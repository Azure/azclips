// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;
using Moq;
using System.Net;

namespace Extensions.MsGraph.Tests;

/// <summary>
/// Tests the customization layer <see cref="ListApplicationProcessor_Orchestrator"/> adds on top of
/// the atomic <c>ListApplicationProcessor</c>: the <c>DisplayName</c> filter shortcut (translated
/// into an OData clause), OData escaping, and combining caller-supplied Filter with the shortcut
/// using OData <c>and</c>. Driven through <c>ProcessAsync</c> (the real entry point) so the request
/// produced by the delegated atomic is observed.
/// </summary>
public class ListApplicationProcessor_OrchestratorTests
{
    private static IEventPublisher NoOpPublisher() => Mock.Of<IEventPublisher>();

    private static (ListApplicationProcessor_Orchestrator orchestrator, FakeGraphHttpPipeline pipeline)
        Create(ListApplicationProcessor_Orchestrator orch, Func<HttpRequestMessage, HttpResponseMessage>? responder = null)
    {
        var pipeline = new FakeGraphHttpPipeline(
            responder ?? (_ => FakeGraphHttpPipeline.JsonResponse("{\"value\":[]}")));
        orch.WithCapturingContext(pipeline, NoOpPublisher());
        return (orch, pipeline);
    }

    [Fact]
    public async Task ProcessAsync_NoInputs_ShouldTargetApplicationsEndpointWithNoQuery()
    {
        var (orch, pipeline) = Create(new ListApplicationProcessor_Orchestrator());

        await orch.ProcessAsync();

        var sent = Assert.Single(pipeline.Sent);
        Assert.Equal(HttpMethod.Get, sent.Method);
        Assert.Equal("https://graph.microsoft.com/v1.0/applications", sent.RequestUri!.ToString());
    }

    [Fact]
    public async Task ProcessAsync_DisplayName_ShouldEmitEqualsFilter()
    {
        var (orch, pipeline) = Create(new ListApplicationProcessor_Orchestrator { DisplayName = "contoso-app" });

        await orch.ProcessAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("displayName eq 'contoso-app'"), uri);
    }

    [Fact]
    public async Task ProcessAsync_FilterAndDisplayName_ShouldCombineWithAnd()
    {
        var (orch, pipeline) = Create(new ListApplicationProcessor_Orchestrator
        {
            Filter = "signInAudience eq 'AzureADMyOrg'",
            DisplayName = "contoso-app",
        });

        await orch.ProcessAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        var expected = "signInAudience eq 'AzureADMyOrg' and displayName eq 'contoso-app'";
        Assert.Contains(Uri.EscapeDataString(expected), uri);
    }

    [Fact]
    public async Task ProcessAsync_ShouldEscapeSingleQuotesInDisplayName()
    {
        var (orch, pipeline) = Create(new ListApplicationProcessor_Orchestrator { DisplayName = "o'brien" });

        await orch.ProcessAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("displayName eq 'o''brien'"), uri);
    }

    [Fact]
    public async Task ProcessAsync_SelectOnly_ShouldPassThrough()
    {
        var (orch, pipeline) = Create(new ListApplicationProcessor_Orchestrator
        {
            Select = new[] { "displayName", "appId" },
        });

        await orch.ProcessAsync();

        var uri = pipeline.Sent.Single().RequestUri!.ToString();
        Assert.Contains("$select=displayName%2CappId", uri);
    }

    [Fact]
    public async Task ProcessAsync_ConsistencyLevel_ShouldFlowIntoAtomicHeader()
    {
        var (orch, pipeline) = Create(new ListApplicationProcessor_Orchestrator
        {
            Search = "\"displayName:contoso\"",
            ConsistencyLevel = "eventual",
        });

        await orch.ProcessAsync();

        var sent = pipeline.Sent.Single();
        Assert.True(sent.Headers.TryGetValues("ConsistencyLevel", out var values));
        Assert.Equal("eventual", values!.Single());
    }

    [Fact]
    public async Task ProcessAsync_AdvancedFilterWithoutConsistencyLevel_ShouldPreserveOriginalGraphMessage()
    {
        const string graphError = "{\"error\":{\"code\":\"Request_UnsupportedQuery\",\"message\":\"Operator 'endsWith' is not supported because the 'ConsistencyLevel:eventual' header is missing.\"}}";
        var (orch, _) = Create(
            new ListApplicationProcessor_Orchestrator { Filter = "endswith(displayName,'app')" },
            _ => FakeGraphHttpPipeline.JsonResponse(graphError, HttpStatusCode.BadRequest));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => orch.ProcessAsync());
        Assert.Contains("'ConsistencyLevel:eventual' header is missing", ex.Message);
    }
}
