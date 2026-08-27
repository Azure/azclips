// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;
using Moq;

namespace Extensions.MsGraph.Tests;

/// <summary>
/// Tests the customization layer <see cref="ListUserProcessor_Orchestrator"/> adds on top of the
/// atomic <c>ListUserProcessor</c>: convenience filter shortcuts (DisplayName, Mail,
/// DisplayNamePrefix, UserPrincipalName), the <c>AppendSelected</c> defaults merge, OData
/// escaping, and combining caller-supplied Filter with the shortcuts using OData <c>and</c>.
/// </summary>
public class ListUserProcessor_OrchestratorTests
{
    private static IEventPublisher NoOpPublisher() => Mock.Of<IEventPublisher>();

    private static (ListUserProcessor_Orchestrator orchestrator, FakeGraphHttpPipeline pipeline)
        Create(ListUserProcessor_Orchestrator orch)
    {
        var pipeline = new FakeGraphHttpPipeline(
            _ => FakeGraphHttpPipeline.JsonResponse("{\"value\":[]}"));
        orch.WithFakeContext(pipeline, NoOpPublisher());
        return (orch, pipeline);
    }

    [Fact]
    public async Task SendAsync_NoInputs_ShouldTargetUsersEndpointWithNoQuery()
    {
        var (orch, pipeline) = Create(new ListUserProcessor_Orchestrator());

        using var _ = await orch.InvokeSendAsync();

        var sent = Assert.Single(pipeline.Sent);
        Assert.Equal(HttpMethod.Get, sent.Method);
        Assert.Equal("https://graph.microsoft.com/v1.0/users", sent.RequestUri!.ToString());
    }

    [Fact]
    public async Task SendAsync_DisplayName_ShouldEmitEqualsFilter()
    {
        var (orch, pipeline) = Create(new ListUserProcessor_Orchestrator { DisplayName = "Ada Lovelace" });

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("displayName eq 'Ada Lovelace'"), uri);
    }

    [Fact]
    public async Task SendAsync_Mail_ShouldEmitEqualsFilter()
    {
        var (orch, pipeline) = Create(new ListUserProcessor_Orchestrator { Mail = "ada@contoso.com" });

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("mail eq 'ada@contoso.com'"), uri);
    }

    [Fact]
    public async Task SendAsync_UserPrincipalName_ShouldEmitEqualsFilter()
    {
        var (orch, pipeline) = Create(new ListUserProcessor_Orchestrator
        {
            UserPrincipalName = "ada@contoso.com",
        });

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("userPrincipalName eq 'ada@contoso.com'"), uri);
    }

    [Fact]
    public async Task SendAsync_DisplayNamePrefix_ShouldEmitStartswithFilter()
    {
        var (orch, pipeline) = Create(new ListUserProcessor_Orchestrator { DisplayNamePrefix = "Ad" });

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("startswith(displayName,'Ad')"), uri);
    }

    [Fact]
    public async Task SendAsync_FilterAndShortcuts_ShouldCombineWithAnd()
    {
        var (orch, pipeline) = Create(new ListUserProcessor_Orchestrator
        {
            Filter = "accountEnabled eq true",
            DisplayName = "Ada",
            Mail = "ada@contoso.com",
        });

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        var expected = "accountEnabled eq true and displayName eq 'Ada' and mail eq 'ada@contoso.com'";
        Assert.Contains(Uri.EscapeDataString(expected), uri);
    }

    [Fact]
    public async Task SendAsync_ShouldEscapeSingleQuotesInFilterShortcuts()
    {
        var (orch, pipeline) = Create(new ListUserProcessor_Orchestrator { DisplayName = "O'Brien" });

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.OriginalString;
        Assert.Contains(Uri.EscapeDataString("displayName eq 'O''Brien'"), uri);
    }

    [Fact]
    public async Task SendAsync_AppendSelected_ShouldMergeDefaultsWithCallerSelect()
    {
        var (orch, pipeline) = Create(new ListUserProcessor_Orchestrator
        {
            Select = new[] { "jobTitle" },
            AppendSelected = true,
        });

        using var _ = await orch.InvokeSendAsync();

        var uri = pipeline.Sent.Single().RequestUri!.ToString();
        Assert.Contains("displayName", uri);
        Assert.Contains("userPrincipalName", uri);
        Assert.Contains("jobTitle", uri);
    }

    [Fact]
    public async Task SendAsync_SelectOnly_ShouldPassThrough()
    {
        var (orch, pipeline) = Create(new ListUserProcessor_Orchestrator
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
        var (orch, pipeline) = Create(new ListUserProcessor_Orchestrator
        {
            Search = "\"displayName:ada\"",
            ConsistencyLevel = "eventual",
        });

        using var _ = await orch.InvokeSendAsync();

        var sent = pipeline.Sent.Single();
        Assert.True(sent.Headers.TryGetValues("ConsistencyLevel", out var values));
        Assert.Equal("eventual", values!.Single());
    }
}
