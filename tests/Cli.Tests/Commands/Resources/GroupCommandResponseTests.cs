// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using ResourcesIndex = Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex;

namespace Cli.Tests.Commands.Resources;

/// <summary>
/// Runs resource group CLI commands through the real engine against canned ARM responses and checks what each command
/// writes, or the error it throws for a response.
/// </summary>
public sealed class GroupCommandResponseTests : IDisposable
{
    private const string ExtensionName = "Resources";
    private const string GroupName = "rg";
    private const string FirstListedGroupName = "rg1";
    private const string SecondListedGroupName = "rg2";
    private const string Location = "westus";
    private const string ApiVersion = "2025-04-01";
    private const string GroupId = $"/subscriptions/{SubscriptionId}/resourceGroups/{GroupName}";
    private const string GroupsUrl = $"{ArmUrl}/subscriptions/{SubscriptionId}/resourcegroups";
    private const string GroupUrl = $"{GroupsUrl}/{GroupName}";
    private const string GroupBody = $$$"""{"id":"{{{GroupId}}}","name":"{{{GroupName}}}","type":"Microsoft.Resources/resourceGroups","location":"{{{Location}}}","managedBy":"me","tags":{"env":"dev"},"properties":{"provisioningState":"Succeeded"}}""";
    private const string ProdTagsGroupBody = $$$"""{"id":"{{{GroupId}}}","name":"{{{GroupName}}}","type":"Microsoft.Resources/resourceGroups","location":"{{{Location}}}","managedBy":"me","tags":{"env":"prod"},"properties":{"provisioningState":"Succeeded"}}""";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public GroupCommandResponseTests() => _engine = CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // ── group create ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GroupCreate_WritesCreatedGroupId()
    {
        _mockWire.RespondTo(HttpMethod.Put, GroupUrl, HttpStatusCode.Created, GroupBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "create", "--name", GroupName, "--location", Location]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(GroupId, Assert.IsType<ResourceGroup>(Assert.Single(_output)).Id);
    }

    // ── group show ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GroupShow_WritesGroupTags()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupUrl, HttpStatusCode.OK, GroupBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "show", "--name", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(new Dictionary<string, string> { ["env"] = "dev" }, Assert.IsType<ResourceGroup>(Assert.Single(_output)).Tags);
    }

    [Fact]
    public async Task RunAsync_GroupShow_WritesGroupProvisioningState()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupUrl, HttpStatusCode.OK, GroupBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "show", "--name", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("Succeeded", Assert.IsType<ResourceGroup>(Assert.Single(_output)).Properties?.ProvisioningState);
    }

    [Fact]
    public async Task RunAsync_GroupShowNotFound_ThrowsStatusUrlAndBody()
    {
        _mockWire.RespondTo(
            HttpMethod.Get,
            GroupUrl,
            HttpStatusCode.NotFound,
            """{"error":{"code":"ResourceGroupNotFound","message":"Resource group 'rg' could not be found."}}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "show", "--name", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            $$$"""404 Not Found from {{{GroupUrl}}}?api-version={{{ApiVersion}}}: {"error":{"code":"ResourceGroupNotFound","message":"Resource group 'rg' could not be found."}}""",
            Assert.IsType<HttpRequestException>(exception).Message);
    }

    // ── group exists ───────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.NoContent, true)]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.NotFound, false)]
    public async Task RunAsync_GroupExistsStatus_WritesWhetherGroupExists(HttpStatusCode status, bool expected)
    {
        _mockWire.RespondTo(HttpMethod.Head, GroupUrl, status);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "exists", "--name", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(expected, Assert.Single(_output));
    }

    [Fact]
    public async Task RunAsync_GroupExistsForbidden_ThrowsStatusAndUrl()
    {
        _mockWire.RespondTo(HttpMethod.Head, GroupUrl, HttpStatusCode.Forbidden);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "exists", "--name", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($"403 Forbidden from {GroupUrl}?api-version={ApiVersion}", Assert.IsType<HttpRequestException>(exception).Message);
    }

    // ── group delete ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GroupDelete_WritesNothing()
    {
        _mockWire.RespondTo(HttpMethod.Delete, GroupUrl, HttpStatusCode.OK);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "delete", "--name", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(_output);
    }

    // ── group list ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GroupListNextLink_WritesGroupsFromEveryPage()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[{"name":"{{FirstListedGroupName}}"}],"nextLink":"{{GroupsUrl}}?api-version={{ApiVersion}}&$skiptoken=p2"}""",
            $$"""{"value":[{"name":"{{SecondListedGroupName}}"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([FirstListedGroupName, SecondListedGroupName], Assert.IsType<List<ResourceGroup>>(Assert.Single(_output)).Select(g => g.Name));
    }

    [Fact]
    public async Task RunAsync_GroupListEmpty_WritesEmptyList()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(Assert.IsType<List<ResourceGroup>>(Assert.Single(_output)));
    }

    // ── group update ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GroupUpdate_WritesUpdatedGroupTags()
    {
        _mockWire.RespondTo(HttpMethod.Patch, GroupUrl, HttpStatusCode.OK, ProdTagsGroupBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["group", "update", "--name", GroupName, "--tags", "env=prod"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(new Dictionary<string, string> { ["env"] = "prod" }, Assert.IsType<ResourceGroup>(Assert.Single(_output)).Tags);
    }
}
