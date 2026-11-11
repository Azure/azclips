// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;
using Moq;
using System.Net;

namespace Extensions.MsGraph.Tests;

/// <summary>
/// Tests the customization layer <see cref="ListUserProcessor_Orchestrator"/> adds on top of the
/// atomic <c>ListUserProcessor</c>: convenience filter shortcuts (DisplayName, Mail,
/// DisplayNamePrefix, UserPrincipalName), the <c>AppendSelected</c> defaults merge, OData
/// escaping, and combining caller-supplied Filter with the shortcuts using OData <c>and</c>.
/// Driven through <c>ProcessAsync</c> (the real entry point) so the request produced by the
/// delegated atomic is observed.
/// </summary>
public class ListUserProcessor_OrchestratorTests
{
    private static IEventPublisher NoOpPublisher() => Mock.Of<IEventPublisher>();

    private static (ListUserProcessor_Orchestrator orchestrator, FakeGraphHttpPipeline pipeline)
        Create(ListUserProcessor_Orchestrator orch, Func<HttpRequestMessage, HttpResponseMessage>? responder = null)
    {
        var pipeline = new FakeGraphHttpPipeline(
            responder ?? (_ => FakeGraphHttpPipeline.JsonResponse("{\"value\":[]}")));
        orch.WithCapturingContext(pipeline, NoOpPublisher());
        return (orch, pipeline);
    }

    [Fact]
    public async Task ProcessAsync_NoInputs_ShouldTargetUsersEndpointWithNoQuery()
    {
        var (orch, pipeline) = Create(new ListUserProcessor_Orchestrator());

        await orch.ProcessAsync();

        var sent = Assert.Single(pipeline.Sent);
        Assert.Equal(HttpMethod.Get, sent.Method);
        Assert.Equal("https://graph.microsoft.com/v1.0/users", sent.RequestUri!.ToString());
    }

    [Fact]
    public async Task ProcessAsync_DisplayName_ShouldEmitEqualsFilter()
    {
        var (orch, pipeline) = Create(new ListUserProcessor_Orchestrator { DisplayName = "Ada Lovelace" });

        await orch.ProcessAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("displayName eq 'Ada Lovelace'"), uri);
    }

    [Fact]
    public async Task ProcessAsync_Mail_ShouldEmitEqualsFilter()
    {
        var (orch, pipeline) = Create(new ListUserProcessor_Orchestrator { Mail = "ada@contoso.com" });

        await orch.ProcessAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("mail eq 'ada@contoso.com'"), uri);
    }

    [Fact]
    public async Task ProcessAsync_UserPrincipalName_ShouldEmitEqualsFilter()
    {
        var (orch, pipeline) = Create(new ListUserProcessor_Orchestrator
        {
            UserPrincipalName = "ada@contoso.com",
        });

        await orch.ProcessAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("userPrincipalName eq 'ada@contoso.com'"), uri);
    }

    [Fact]
    public async Task ProcessAsync_DisplayNamePrefix_ShouldEmitStartswithFilter()
    {
        var (orch, pipeline) = Create(new ListUserProcessor_Orchestrator { DisplayNamePrefix = "Ad" });

        await orch.ProcessAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("startswith(displayName,'Ad')"), uri);
    }

    [Fact]
    public async Task ProcessAsync_FilterAndShortcuts_ShouldCombineWithAnd()
    {
        var (orch, pipeline) = Create(new ListUserProcessor_Orchestrator
        {
            Filter = "accountEnabled eq true",
            DisplayName = "Ada",
            Mail = "ada@contoso.com",
        });

        await orch.ProcessAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        var expected = "accountEnabled eq true and displayName eq 'Ada' and mail eq 'ada@contoso.com'";
        Assert.Contains(Uri.EscapeDataString(expected), uri);
    }

    [Fact]
    public async Task ProcessAsync_ShouldEscapeSingleQuotesInFilterShortcuts()
    {
        var (orch, pipeline) = Create(new ListUserProcessor_Orchestrator { DisplayName = "O'Brien" });

        await orch.ProcessAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("displayName eq 'O''Brien'"), uri);
    }

    [Fact]
    public async Task ProcessAsync_AppendSelected_ShouldMergeDefaultsWithCallerSelect()
    {
        var (orch, pipeline) = Create(new ListUserProcessor_Orchestrator
        {
            Select = new[] { "jobTitle" },
            AppendSelected = true,
        });

        await orch.ProcessAsync();

        var uri = pipeline.Sent.Single().RequestUri!.ToString();
        Assert.Contains("displayName", uri);
        Assert.Contains("userPrincipalName", uri);
        Assert.Contains("jobTitle", uri);
    }

    [Fact]
    public async Task ProcessAsync_SelectOnly_ShouldPassThrough()
    {
        var (orch, pipeline) = Create(new ListUserProcessor_Orchestrator
        {
            Select = new[] { "id", "displayName" },
        });

        await orch.ProcessAsync();

        var uri = pipeline.Sent.Single().RequestUri!.ToString();
        Assert.Contains("$select=id%2CdisplayName", uri);
    }

    [Fact]
    public async Task ProcessAsync_ConsistencyLevel_ShouldFlowIntoAtomicHeader()
    {
        var (orch, pipeline) = Create(new ListUserProcessor_Orchestrator
        {
            Search = "\"displayName:ada\"",
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
            new ListUserProcessor_Orchestrator { Filter = "endswith(mail,'@contoso.com')" },
            _ => FakeGraphHttpPipeline.JsonResponse(graphError, HttpStatusCode.BadRequest));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => orch.ProcessAsync());
        Assert.Contains("'ConsistencyLevel:eventual' header is missing", ex.Message);
    }
}
