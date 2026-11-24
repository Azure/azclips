// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using MsGraphIndex = Microsoft.Azure.Azclips.Extensions.MsGraph.ExtensionIndex;

namespace Cli.Tests.Commands.MsGraph;

/// <summary>
/// Runs MsGraph CLI commands through the real engine against canned Graph responses and checks what each
/// command writes, or the error it throws. The HTTP wire is a strict mock, so a request a test did not set
/// up a response for throws <see cref="MockException"/>.
/// </summary>
public sealed class MsGraphCommandResponseTests : IDisposable
{
    private const string ExtensionName = "MsGraph";
    private const string UserId = "aaaaaaaa-0000-0000-0000-000000000001";
    private const string GroupId = "bbbbbbbb-0000-0000-0000-000000000002";
    private const string MemberId = "dddddddd-0000-0000-0000-000000000004";
    private const string AppId = "eeeeeeee-0000-0000-0000-000000000005";
    private const string UserDisplayName = "Ada Lovelace";
    private const string GroupName = "Sales";
    private const string AppDisplayName = "Contoso";
    private const string GraphUrl = "https://graph.microsoft.com/v1.0";
    private const string UsersUrl = GraphUrl + "/users";
    private const string GroupsUrl = GraphUrl + "/groups";
    private const string ApplicationsUrl = GraphUrl + "/applications";
    private const string GroupMembersUrl = $"{GroupsUrl}/{GroupId}/members";
    private const string AppUrl = $"{ApplicationsUrl}/{AppId}";
    private const string CheckMemberGroupsUrl = $"{GraphUrl}/directoryObjects/{MemberId}/checkMemberGroups";
    private const string AppObject = $$"""{"id":"{{AppId}}","displayName":"{{AppDisplayName}}"}""";
    private const string MembersPageWithNextLink = $$"""{"value":[{"id":"m1"}],"@odata.nextLink":"{{GroupMembersUrl}}?$skiptoken=page2"}""";
    private const string ForbiddenError = """{"error":{"code":"Authorization_RequestDenied"}}""";
    private const string NotFoundError = """{"error":{"code":"Request_ResourceNotFound"}}""";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public MsGraphCommandResponseTests() => _engine = CreateEngine(typeof(MsGraphIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // Each command that resolves a group name with a lookup on /groups, and the name it looks up.
    public static TheoryData<string[], string> GetGroupNameLookups() => new()
    {
        { ["msgraph", "group", "show", "--group", GroupName], GroupName },
        { ["msgraph", "group", "member", "list", "--group", GroupName], GroupName },
        { ["msgraph", "group", "member", "list", "--display-name", "Sales Team"], "Sales Team" },
        { ["msgraph", "group", "member", "check", "--group", GroupName, "--member-id", MemberId], GroupName },
    };

    // ── list output ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_UserListEmptyValue_WritesEmptyList()
    {
        _mockWire.RespondTo(HttpMethod.Get, UsersUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "user", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(Assert.IsType<List<User>>(Assert.Single(_output)));
    }

    [Fact]
    public async Task RunAsync_GroupListEmptyValue_WritesEmptyList()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(Assert.IsType<List<Group>>(Assert.Single(_output)));
    }

    [Fact]
    public async Task RunAsync_AppListEmptyValue_WritesEmptyList()
    {
        _mockWire.RespondTo(HttpMethod.Get, ApplicationsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "app", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(Assert.IsType<List<Application>>(Assert.Single(_output)));
    }

    [Fact]
    public async Task RunAsync_GroupMemberListEmptyValue_WritesEmptyList()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupMembersUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "member", "list", "--group", GroupId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(Assert.IsType<List<DirectoryObject>>(Assert.Single(_output)));
    }

    // Current behaviour: a body with no value array is treated like an empty page rather than an error.
    [Fact]
    public async Task RunAsync_GroupListBodyWithoutValue_WritesEmptyList()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.OK, "{}");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(Assert.IsType<List<Group>>(Assert.Single(_output)));
    }

    [Fact]
    public async Task RunAsync_UserListOnePage_WritesUsersFromValue()
    {
        _mockWire.RespondTo(HttpMethod.Get, UsersUrl, HttpStatusCode.OK, """{"value":[{"id":"u1"},{"id":"u2"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "user", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(["u1", "u2"], Assert.IsType<List<User>>(Assert.Single(_output)).Select(u => u.Id));
    }

    [Fact]
    public async Task RunAsync_GroupListOnePage_WritesGroupsFromValue()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.OK, """{"value":[{"id":"g1"},{"id":"g2"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(["g1", "g2"], Assert.IsType<List<Group>>(Assert.Single(_output)).Select(g => g.Id));
    }

    [Fact]
    public async Task RunAsync_AppListOnePage_WritesApplicationsFromValue()
    {
        _mockWire.RespondTo(HttpMethod.Get, ApplicationsUrl, HttpStatusCode.OK, """{"value":[{"id":"a1"},{"id":"a2"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "app", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(["a1", "a2"], Assert.IsType<List<Application>>(Assert.Single(_output)).Select(a => a.Id));
    }

    [Fact]
    public async Task RunAsync_GroupMemberListOnePage_WritesMembersFromValue()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupMembersUrl, HttpStatusCode.OK, """{"value":[{"id":"m1"},{"id":"m2"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "member", "list", "--group", GroupId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(["m1", "m2"], Assert.IsType<List<DirectoryObject>>(Assert.Single(_output)).Select(m => m.Id));
    }

    [Fact]
    public async Task RunAsync_GroupMemberList_WritesMemberODataType()
    {
        _mockWire.RespondTo(
            HttpMethod.Get,
            GroupMembersUrl,
            HttpStatusCode.OK,
            """{"value":[{"id":"m1","@odata.type":"#microsoft.graph.servicePrincipal"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "member", "list", "--group", GroupId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("#microsoft.graph.servicePrincipal", Assert.Single(Assert.IsType<List<DirectoryObject>>(Assert.Single(_output))).OdataType);
    }

    // ── paging ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_UserListThreePages_WritesAllUsers()
    {
        RespondWithThreeUserPages();
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "user", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(["u1", "u2", "u3", "u4"], Assert.IsType<List<User>>(Assert.Single(_output)).Select(u => u.Id));
    }

    [Fact]
    public async Task RunAsync_UserListThreePages_RequestsEachNextLinkAsGiven()
    {
        RespondWithThreeUserPages();
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "user", "list", "--display-name", "Ada"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            [
                "GET /v1.0/users?$filter=startswith(displayName,'Ada')",
                "GET /v1.0/users?$skiptoken=page2",
                "GET /v1.0/users?$skiptoken=page3",
            ],
            _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_UserListNextPage_KeepsConsistencyLevelHeader()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[{"id":"u1"}],"@odata.nextLink":"{{UsersUrl}}?$skiptoken=page2"}""",
            """{"value":[{"id":"u2"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "user", "list", "--consistency-level", "eventual"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("eventual", _mockWire.SentRequests()[1].HeaderValues("ConsistencyLevel"));
    }

    [Fact]
    public async Task RunAsync_GroupListTwoPages_WritesAllGroups()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[{"id":"g1"}],"@odata.nextLink":"{{GroupsUrl}}?$skiptoken=page2"}""",
            """{"value":[{"id":"g2"},{"id":"g3"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(["g1", "g2", "g3"], Assert.IsType<List<Group>>(Assert.Single(_output)).Select(g => g.Id));
    }

    [Fact]
    public async Task RunAsync_AppListTwoPages_WritesAllApplications()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[{"id":"a1"}],"@odata.nextLink":"{{ApplicationsUrl}}?$skiptoken=page2"}""",
            """{"value":[{"id":"a2"},{"id":"a3"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "app", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(["a1", "a2", "a3"], Assert.IsType<List<Application>>(Assert.Single(_output)).Select(a => a.Id));
    }

    [Fact]
    public async Task RunAsync_GroupMemberListTwoPages_WritesAllMembers()
    {
        _mockWire.RespondInOrder(MembersPageWithNextLink, """{"value":[{"id":"m2"},{"id":"m3"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "member", "list", "--group", GroupId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(["m1", "m2", "m3"], Assert.IsType<List<DirectoryObject>>(Assert.Single(_output)).Select(m => m.Id));
    }

    [Fact]
    public async Task RunAsync_GroupMemberListGroupNameTwoPages_LooksUpGroupOnceThenFollowsNextLink()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[{"id":"{{GroupId}}"}]}""",
            MembersPageWithNextLink,
            """{"value":[{"id":"m2"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "member", "list", "--group", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            [
                $"GET /v1.0/groups?$filter=startswith(displayName,'{GroupName}')&$select=id,displayName",
                $"GET /v1.0/groups/{GroupId}/members",
                $"GET /v1.0/groups/{GroupId}/members?$skiptoken=page2",
            ],
            _mockWire.FullRequestLines());
    }

    // ── single-object output ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_UserShow_WritesUser()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{UsersUrl}/{UserId}", HttpStatusCode.OK, $$"""{"id":"{{UserId}}","displayName":"{{UserDisplayName}}"}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "user", "show", "--id", UserId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(UserDisplayName, Assert.IsType<User>(Assert.Single(_output)).DisplayName);
    }

    [Fact]
    public async Task RunAsync_GroupShow_WritesGroup()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{GroupsUrl}/{GroupId}", HttpStatusCode.OK, $$"""{"id":"{{GroupId}}","displayName":"{{GroupName}}"}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "show", "--group", GroupId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(GroupName, Assert.IsType<Group>(Assert.Single(_output)).DisplayName);
    }

    [Fact]
    public async Task RunAsync_AppShow_WritesApplication()
    {
        _mockWire.RespondTo(HttpMethod.Get, AppUrl, HttpStatusCode.OK, AppObject);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "app", "show", "--id", AppId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(AppDisplayName, Assert.IsType<Application>(Assert.Single(_output)).DisplayName);
    }

    [Fact]
    public async Task RunAsync_AppCreate_WritesCreatedApplication()
    {
        _mockWire.RespondTo(HttpMethod.Post, ApplicationsUrl, HttpStatusCode.Created, AppObject);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "app", "create", "--display-name", AppDisplayName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(AppId, Assert.IsType<Application>(Assert.Single(_output)).Id);
    }

    [Fact]
    public async Task RunAsync_AppDeleteNoContent_WritesNothing()
    {
        _mockWire.RespondTo(HttpMethod.Delete, AppUrl, HttpStatusCode.NoContent);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "app", "delete", "--id", AppId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(_output);
    }

    [Fact]
    public async Task RunAsync_GroupMemberCheckMember_WritesMatchingGroupIds()
    {
        _mockWire.RespondTo(HttpMethod.Post, CheckMemberGroupsUrl, HttpStatusCode.OK, $$"""{"value":["{{GroupId}}"]}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "member", "check", "--group", GroupId, "--member-id", MemberId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([GroupId], Assert.IsType<CheckMemberGroupsResponse>(Assert.Single(_output)).Value!);
    }

    // Current behaviour: a non-member is reported as an empty id list, not as false.
    [Fact]
    public async Task RunAsync_GroupMemberCheckNotMember_WritesEmptyGroupIds()
    {
        _mockWire.RespondTo(HttpMethod.Post, CheckMemberGroupsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "member", "check", "--group", GroupId, "--member-id", MemberId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(Assert.IsType<CheckMemberGroupsResponse>(Assert.Single(_output)).Value!);
    }

    // ── group name lookup errors ───────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GetGroupNameLookups))]
    public async Task RunAsync_GroupNameWithNoMatch_ThrowsInvalidInput(string[] commandLine, string name)
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($"Group '{name}' is not found in Graph.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Theory]
    [MemberData(nameof(GetGroupNameLookups))]
    public async Task RunAsync_GroupNameWithTwoMatches_ThrowsInvalidInput(string[] commandLine, string name)
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.OK, """{"value":[{"id":"g1"},{"id":"g2"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            $"More than one group matched '{name}'. Use the object id instead.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Theory]
    [MemberData(nameof(GetGroupNameLookups))]
    public async Task RunAsync_GroupNameMatchWithoutId_ThrowsInvalidOperation(string[] commandLine, string name)
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.OK, $$"""{"value":[{"displayName":"{{name}}"}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($"Group resolved for '{name}' has no id.", Assert.IsType<InvalidOperationException>(exception).Message);
    }

    // ── Graph error responses ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_AppShowNotFound_ThrowsHttpRequestExceptionWithStatusUrlAndBody()
    {
        _mockWire.RespondTo(HttpMethod.Get, AppUrl, HttpStatusCode.NotFound, NotFoundError);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "app", "show", "--id", AppId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($"404 Not Found from {AppUrl}: {NotFoundError}", Assert.IsType<HttpRequestException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_UserListForbidden_ThrowsHttpRequestExceptionWithEncodedUrl()
    {
        _mockWire.RespondTo(HttpMethod.Get, UsersUrl, HttpStatusCode.Forbidden, ForbiddenError);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "user", "list", "--display-name", "Ada"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "403 Forbidden from https://graph.microsoft.com/v1.0/users?$filter=startswith%28displayName%2C%27Ada%27%29: " + ForbiddenError,
            Assert.IsType<HttpRequestException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_AppDeleteNotFoundWithoutBody_ThrowsHttpRequestExceptionWithoutBody()
    {
        _mockWire.RespondTo(HttpMethod.Delete, AppUrl, HttpStatusCode.NotFound);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "app", "delete", "--id", AppId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal($"404 Not Found from {AppUrl}", Assert.IsType<HttpRequestException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_GroupShowLookupForbidden_ThrowsHttpRequestExceptionFromLookup()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.Forbidden, ForbiddenError);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "show", "--group", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "403 Forbidden from https://graph.microsoft.com/v1.0/groups?$filter=startswith%28displayName%2C%27Sales%27%29&$select=id%2CdisplayName: " + ForbiddenError,
            Assert.IsType<HttpRequestException>(exception).Message);
    }

    private void RespondWithThreeUserPages() =>
        _mockWire.RespondInOrder(
            $$"""{"value":[{"id":"u1"},{"id":"u2"}],"@odata.nextLink":"{{UsersUrl}}?$skiptoken=page2"}""",
            $$"""{"value":[{"id":"u3"}],"@odata.nextLink":"{{UsersUrl}}?$skiptoken=page3"}""",
            """{"value":[{"id":"u4"}]}""");
}
