// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using MsGraphIndex = Microsoft.Azure.Azclips.Extensions.MsGraph.ExtensionIndex;

namespace Cli.Tests.Commands.MsGraph;

/// <summary>
/// Runs MsGraph CLI commands through the real engine and checks the request each one sends to Graph: method,
/// path, decoded query values, headers and JSON body. The wire mock is strict, so an unexpected request fails the test.
/// </summary>
public sealed class MsGraphCommandRequestTests : IDisposable
{
    private const string ExtensionName = "MsGraph";
    private const string UserId = "aaaaaaaa-0000-0000-0000-000000000001";
    private const string GroupId = "bbbbbbbb-0000-0000-0000-000000000002";
    private const string ResolvedGroupId = "cccccccc-0000-0000-0000-000000000003";
    private const string MemberId = "dddddddd-0000-0000-0000-000000000004";
    private const string AppId = "eeeeeeee-0000-0000-0000-000000000005";
    private const string Upn = "ada@contoso.com";
    private const string GroupName = "Sales";
    private const string AppDisplayName = "Contoso";
    private const string DefaultUserProperties = "displayName,id,deletedDateTime,userPrincipalName,usageLocation,givenName,surname,accountEnabled,mailNickname,mail";
    private const string DefaultGroupProperties = "displayName,id,deletedDateTime,securityEnabled,mailEnabled,mailNickname,description";
    private const string GraphUrl = "https://graph.microsoft.com/v1.0";
    private const string UsersUrl = GraphUrl + "/users";
    private const string GroupsUrl = GraphUrl + "/groups";
    private const string ApplicationsUrl = GraphUrl + "/applications";
    private const string AppUrl = $"{ApplicationsUrl}/{AppId}";
    private const string CheckMemberGroupsUrl = $"{GraphUrl}/directoryObjects/{MemberId}/checkMemberGroups";
    private const string AppObject = $$"""{"id":"{{AppId}}"}""";
    private const string ResolvedGroupLookup = $$"""{"value":[{"id":"{{ResolvedGroupId}}"}]}""";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public MsGraphCommandRequestTests()
    {
        _engine = CreateEngine(typeof(MsGraphIndex).Assembly, _mockWire);

        _mockWire.RespondTo(HttpMethod.Get, UsersUrl, HttpStatusCode.OK, EmptyList);
        _mockWire.RespondTo(HttpMethod.Get, $"{UsersUrl}/{UserId}", HttpStatusCode.OK, $$"""{"id":"{{UserId}}"}""");
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.OK, EmptyList);
        _mockWire.RespondTo(HttpMethod.Get, $"{GroupsUrl}/{GroupId}", HttpStatusCode.OK, $$"""{"id":"{{GroupId}}"}""");
        _mockWire.RespondTo(HttpMethod.Get, $"{GroupsUrl}/{GroupId}/members", HttpStatusCode.OK, EmptyList);
        _mockWire.RespondTo(HttpMethod.Get, $"{GroupsUrl}/{ResolvedGroupId}", HttpStatusCode.OK, $$"""{"id":"{{ResolvedGroupId}}"}""");
        _mockWire.RespondTo(HttpMethod.Get, $"{GroupsUrl}/{ResolvedGroupId}/members", HttpStatusCode.OK, EmptyList);
        _mockWire.RespondTo(HttpMethod.Post, CheckMemberGroupsUrl, HttpStatusCode.OK, EmptyList);
        _mockWire.RespondTo(HttpMethod.Get, ApplicationsUrl, HttpStatusCode.OK, EmptyList);
        _mockWire.RespondTo(HttpMethod.Post, ApplicationsUrl, HttpStatusCode.Created, AppObject);
    }

    public void Dispose() => _engine.Dispose();

    // Each list command with the arguments it needs.
    public static TheoryData<string[]> GetListCommands() => new()
    {
        { ["msgraph", "user", "list"] },
        { ["msgraph", "group", "list"] },
        { ["msgraph", "app", "list"] },
        { ["msgraph", "group", "member", "list", "--group", GroupId] },
    };

    [Theory]
    [MemberData(nameof(GetListCommands))]
    public async Task RunAsync_ListWithoutOptions_SendsRequestWithoutQueryString(string[] commandLine)
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(string.Empty, Assert.Single(_mockWire.SentRequests()).RequestUri!.Query);
    }

    [Theory]
    [MemberData(nameof(GetListCommands))]
    public async Task RunAsync_ListConsistencyLevel_SendsConsistencyLevelHeader(string[] commandLine)
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, [.. commandLine, "--consistency-level", "eventual"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("eventual", Assert.Single(_mockWire.SentRequests()).HeaderValues("ConsistencyLevel"));
    }

    [Theory]
    [MemberData(nameof(GetListCommands))]
    public async Task RunAsync_ListWithoutConsistencyLevel_SendsNoConsistencyLevelHeader(string[] commandLine)
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Null(Assert.Single(_mockWire.SentRequests()).HeaderValues("ConsistencyLevel"));
    }

    // ── user show ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_UserShowObjectId_SendsSingleGetToUserPath()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "user", "show", "--id", UserId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET /v1.0/users/{UserId}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_UserShowUserPrincipalName_GetsUserByEscapedUpn()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{UsersUrl}/{Upn}", HttpStatusCode.OK, $$"""{"id":"{{UserId}}","userPrincipalName":"{{Upn}}"}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "user", "show", "--id", Upn]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("/v1.0/users/ada%40contoso.com", Assert.Single(_mockWire.SentRequests()).RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task RunAsync_UserShowSelect_SendsSelectedProperties()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "user", "show", "--id", UserId, "--select", "jobTitle", "mail"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("jobTitle,mail", Assert.Single(_mockWire.SentRequests()).QueryValue("$select"));
    }

    [Fact]
    public async Task RunAsync_UserShowAppendSelectedWithoutSelect_SelectsDefaultProperties()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "user", "show", "--id", UserId, "--append-selected"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(DefaultUserProperties, Assert.Single(_mockWire.SentRequests()).QueryValue("$select"));
    }

    [Fact]
    public async Task RunAsync_UserShowAppendSelectedWithSelect_AppendsNewPropertiesAfterDefaults()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "user", "show", "--id", UserId, "--select", "jobTitle", "ID", "--append-selected"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal($"{DefaultUserProperties},jobTitle", Assert.Single(_mockWire.SentRequests()).QueryValue("$select"));
    }

    [Fact]
    public async Task RunAsync_UserShowExpand_SendsExpandedProperties()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "user", "show", "--id", UserId, "--expand", "memberOf", "manager"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("memberOf,manager", Assert.Single(_mockWire.SentRequests()).QueryValue("$expand"));
    }

    // ── user list ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_UserListDisplayName_FiltersByDisplayNamePrefix()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "user", "list", "--display-name", "Ada"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("startswith(displayName,'Ada')", Assert.Single(_mockWire.SentRequests()).QueryValue("$filter"));
    }

    [Fact]
    public async Task RunAsync_UserListDisplayNameWithQuote_DoublesQuote()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "user", "list", "--display-name", "O'Brien"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("startswith(displayName,'O''Brien')", Assert.Single(_mockWire.SentRequests()).QueryValue("$filter"));
    }

    [Fact]
    public async Task RunAsync_UserListUpn_FiltersByUserPrincipalName()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "user", "list", "--upn", Upn]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("userPrincipalName eq 'ada@contoso.com'", Assert.Single(_mockWire.SentRequests()).QueryValue("$filter"));
    }

    [Fact]
    public async Task RunAsync_UserListFilter_SendsFilterUnchanged()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "user", "list", "--filter", "accountEnabled eq true"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("accountEnabled eq true", Assert.Single(_mockWire.SentRequests()).QueryValue("$filter"));
    }

    [Fact]
    public async Task RunAsync_UserListFilterWithShortcuts_JoinsFilterThenDisplayNameThenUpnWithAnd()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "user", "list", "--upn", Upn, "--filter", "accountEnabled eq true", "--display-name", "Ad"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            "accountEnabled eq true and startswith(displayName,'Ad') and userPrincipalName eq 'ada@contoso.com'",
            Assert.Single(_mockWire.SentRequests()).QueryValue("$filter"));
    }

    // Current behaviour: the --filter text is not wrapped in parentheses, so an 'or' in it binds looser
    // than the 'and' added for the shortcut and the shortcut no longer restricts every result.
    [Fact]
    public async Task RunAsync_UserListFilterWithOrAndShortcut_DoesNotParenthesizeFilter()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "user", "list", "--filter", "city eq 'Oslo' or city eq 'Bergen'", "--display-name", "Ad"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            "city eq 'Oslo' or city eq 'Bergen' and startswith(displayName,'Ad')",
            Assert.Single(_mockWire.SentRequests()).QueryValue("$filter"));
    }

    [Fact]
    public async Task RunAsync_UserListOrderbyTwoValues_JoinsWithComma()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "user", "list", "--orderby", "displayName", "mail desc"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("displayName,mail desc", Assert.Single(_mockWire.SentRequests()).QueryValue("$orderby"));
    }

    // Current behaviour: the value is sent as typed; Graph needs $search wrapped in double quotes, which the command does not add.
    [Fact]
    public async Task RunAsync_UserListSearch_SendsSearchAsTyped()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "user", "list", "--search", "displayName:ada"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("displayName:ada", Assert.Single(_mockWire.SentRequests()).QueryValue("$search"));
    }

    [Fact]
    public async Task RunAsync_UserListSelect_SendsSelectedProperties()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "user", "list", "--select", "id", "jobTitle"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("id,jobTitle", Assert.Single(_mockWire.SentRequests()).QueryValue("$select"));
    }

    [Fact]
    public async Task RunAsync_UserListAppendSelected_AppendsNewPropertiesAfterDefaults()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "user", "list", "--select", "jobTitle", "mail", "--append-selected"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal($"{DefaultUserProperties},jobTitle", Assert.Single(_mockWire.SentRequests()).QueryValue("$select"));
    }

    [Fact]
    public async Task RunAsync_UserListExpand_SendsExpandedProperties()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "user", "list", "--expand", "memberOf"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("memberOf", Assert.Single(_mockWire.SentRequests()).QueryValue("$expand"));
    }

    // ── group show ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GroupShowObjectId_GetsGroupWithoutLookup()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "show", "--group", GroupId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET /v1.0/groups/{GroupId}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_GroupShowDisplayName_LooksUpGroupByPrefixThenGetsIt()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.OK, ResolvedGroupLookup);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "show", "--group", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            [
                $"GET /v1.0/groups?$filter=startswith(displayName,'{GroupName}')&$select=id,displayName",
                $"GET /v1.0/groups/{ResolvedGroupId}",
            ],
            _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_GroupShowDisplayNameWithQueryOptions_AppliesThemToGroupRequestOnly()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.OK, ResolvedGroupLookup);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "show", "--group", GroupName, "--select", "mail", "--expand", "members"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            [
                $"GET /v1.0/groups?$filter=startswith(displayName,'{GroupName}')&$select=id,displayName",
                $"GET /v1.0/groups/{ResolvedGroupId}?$select=mail&$expand=members",
            ],
            _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_GroupShowSelect_SendsSelectedProperties()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "show", "--group", GroupId, "--select", "mail", "visibility"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("mail,visibility", Assert.Single(_mockWire.SentRequests()).QueryValue("$select"));
    }

    [Fact]
    public async Task RunAsync_GroupShowAppendSelected_AppendsNewPropertiesAfterDefaults()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "show", "--group", GroupId, "--select", "mail", "id", "--append-selected"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal($"{DefaultGroupProperties},mail", Assert.Single(_mockWire.SentRequests()).QueryValue("$select"));
    }

    [Fact]
    public async Task RunAsync_GroupShowAppendSelectedWithoutSelect_SelectsDefaultProperties()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "show", "--group", GroupId, "--append-selected"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(DefaultGroupProperties, Assert.Single(_mockWire.SentRequests()).QueryValue("$select"));
    }

    [Fact]
    public async Task RunAsync_GroupShowExpand_SendsExpandedProperties()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "show", "--group", GroupId, "--expand", "members", "owners"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("members,owners", Assert.Single(_mockWire.SentRequests()).QueryValue("$expand"));
    }

    // ── group list ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GroupListDisplayName_FiltersByDisplayNamePrefix()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "list", "--display-name", "Sa"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("startswith(displayName,'Sa')", Assert.Single(_mockWire.SentRequests()).QueryValue("$filter"));
    }

    [Fact]
    public async Task RunAsync_GroupListFilterWithDisplayName_JoinsFilterThenDisplayNameWithAnd()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "list", "--display-name", "Sa", "--filter", "securityEnabled eq true"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            "securityEnabled eq true and startswith(displayName,'Sa')",
            Assert.Single(_mockWire.SentRequests()).QueryValue("$filter"));
    }

    [Fact]
    public async Task RunAsync_GroupListOrderbyTwoValues_JoinsWithComma()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "list", "--orderby", "displayName", "createdDateTime desc"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("displayName,createdDateTime desc", Assert.Single(_mockWire.SentRequests()).QueryValue("$orderby"));
    }

    [Fact]
    public async Task RunAsync_GroupListSearch_SendsSearchAsTyped()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "list", "--search", "\"displayName:contoso\""]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("\"displayName:contoso\"", Assert.Single(_mockWire.SentRequests()).QueryValue("$search"));
    }

    [Fact]
    public async Task RunAsync_GroupListSelect_SendsSelectedProperties()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "list", "--select", "id", "mail"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("id,mail", Assert.Single(_mockWire.SentRequests()).QueryValue("$select"));
    }

    [Fact]
    public async Task RunAsync_GroupListExpand_SendsExpandedProperties()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "list", "--expand", "members"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("members", Assert.Single(_mockWire.SentRequests()).QueryValue("$expand"));
    }

    // ── group member list ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GroupMemberListObjectId_ListsMembersWithoutLookup()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "member", "list", "--group", GroupId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET /v1.0/groups/{GroupId}/members"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_GroupMemberListGroupName_LooksUpGroupByPrefixThenListsMembers()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.OK, ResolvedGroupLookup);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "member", "list", "--group", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            [
                $"GET /v1.0/groups?$filter=startswith(displayName,'{GroupName}')&$select=id,displayName",
                $"GET /v1.0/groups/{ResolvedGroupId}/members",
            ],
            _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_GroupMemberListDisplayName_LooksUpGroupByExactNameThenListsMembers()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.OK, ResolvedGroupLookup);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "member", "list", "--display-name", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            [
                $"GET /v1.0/groups?$filter=displayName eq '{GroupName}'&$select=id,displayName",
                $"GET /v1.0/groups/{ResolvedGroupId}/members",
            ],
            _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_GroupMemberListDisplayNameWithQuote_DoublesQuoteInLookup()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.OK, ResolvedGroupLookup);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "member", "list", "--display-name", "O'Neil Team"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("displayName eq 'O''Neil Team'", _mockWire.SentRequests()[0].QueryValue("$filter"));
    }

    [Fact]
    public async Task RunAsync_GroupMemberListGroupNameWithQueryOptions_AppliesThemToMembersRequestOnly()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.OK, ResolvedGroupLookup);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "member", "list", "--group", GroupName, "--select", "id", "--orderby", "displayName"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            [
                $"GET /v1.0/groups?$filter=startswith(displayName,'{GroupName}')&$select=id,displayName",
                $"GET /v1.0/groups/{ResolvedGroupId}/members?$select=id&$orderby=displayName",
            ],
            _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_GroupMemberListFilter_SendsFilterUnchanged()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "member", "list", "--group", GroupId, "--filter", "startswith(displayName,'A')"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("startswith(displayName,'A')", Assert.Single(_mockWire.SentRequests()).QueryValue("$filter"));
    }

    [Fact]
    public async Task RunAsync_GroupMemberListSelect_SendsSelectedPropertiesWithoutDefaults()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "member", "list", "--group", GroupId, "--select", "id", "displayName"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("id,displayName", Assert.Single(_mockWire.SentRequests()).QueryValue("$select"));
    }

    [Fact]
    public async Task RunAsync_GroupMemberListExpand_SendsExpandedProperties()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "member", "list", "--group", GroupId, "--expand", "manager"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("manager", Assert.Single(_mockWire.SentRequests()).QueryValue("$expand"));
    }

    [Fact]
    public async Task RunAsync_GroupMemberListOrderbyTwoValues_JoinsWithComma()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "member", "list", "--group", GroupId, "--orderby", "displayName", "id desc"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("displayName,id desc", Assert.Single(_mockWire.SentRequests()).QueryValue("$orderby"));
    }

    [Fact]
    public async Task RunAsync_GroupMemberListSearch_SendsSearchAsTyped()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "member", "list", "--group", GroupId, "--search", "\"displayName:ada\""]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("\"displayName:ada\"", Assert.Single(_mockWire.SentRequests()).QueryValue("$search"));
    }

    // ── group member check ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GroupMemberCheckObjectId_PostsCheckMemberGroupsWithoutLookup()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "member", "check", "--group", GroupId, "--member-id", MemberId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"POST /v1.0/directoryObjects/{MemberId}/checkMemberGroups"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_GroupMemberCheckObjectId_SendsGroupIdInBody()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Post, CheckMemberGroupsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "member", "check", "--group", GroupId, "--member-id", MemberId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal($$"""{"groupIds":["{{GroupId}}"]}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_GroupMemberCheckGroupName_LooksUpGroupByPrefixThenPostsCheckMemberGroups()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.OK, ResolvedGroupLookup);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "member", "check", "--group", GroupName, "--member-id", MemberId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            [
                $"GET /v1.0/groups?$filter=startswith(displayName,'{GroupName}')&$select=id,displayName",
                $"POST /v1.0/directoryObjects/{MemberId}/checkMemberGroups",
            ],
            _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_GroupMemberCheckGroupName_SendsResolvedGroupIdInBody()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupsUrl, HttpStatusCode.OK, ResolvedGroupLookup);
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Post, CheckMemberGroupsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "group", "member", "check", "--group", GroupName, "--member-id", MemberId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal($$"""{"groupIds":["{{ResolvedGroupId}}"]}""", Assert.Single(sentBodies));
    }

    // ── app create ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_AppCreate_SendsPostToApplications()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "app", "create", "--display-name", AppDisplayName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(["POST /v1.0/applications"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_AppCreateWithoutSignInAudience_SendsOnlyDisplayName()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Post, ApplicationsUrl, HttpStatusCode.Created, AppObject);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "app", "create", "--display-name", AppDisplayName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"displayName":"Contoso"}""", Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_AppCreateSignInAudience_SendsSignInAudience()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Post, ApplicationsUrl, HttpStatusCode.Created, AppObject);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "app", "create", "--display-name", AppDisplayName, "--sign-in-audience", "AzureADMyOrg"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("""{"displayName":"Contoso","signInAudience":"AzureADMyOrg"}""", Assert.Single(sentBodies));
    }

    // ── app show / app delete ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_AppShow_SendsGetToApplicationPath()
    {
        _mockWire.RespondTo(HttpMethod.Get, AppUrl, HttpStatusCode.OK, AppObject);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "app", "show", "--id", AppId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET /v1.0/applications/{AppId}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_AppDelete_SendsDeleteToApplicationPath()
    {
        _mockWire.RespondTo(HttpMethod.Delete, AppUrl, HttpStatusCode.NoContent);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "app", "delete", "--id", AppId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"DELETE /v1.0/applications/{AppId}"], _mockWire.FullRequestLines());
    }

    // ── app list ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_AppListDisplayName_FiltersByExactDisplayName()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "app", "list", "--display-name", AppDisplayName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("displayName eq 'Contoso'", Assert.Single(_mockWire.SentRequests()).QueryValue("$filter"));
    }

    [Fact]
    public async Task RunAsync_AppListFilterWithDisplayName_JoinsFilterThenDisplayNameWithAnd()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "app", "list", "--display-name", AppDisplayName, "--filter", "signInAudience eq 'AzureADMyOrg'"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            "signInAudience eq 'AzureADMyOrg' and displayName eq 'Contoso'",
            Assert.Single(_mockWire.SentRequests()).QueryValue("$filter"));
    }

    [Fact]
    public async Task RunAsync_AppListSelect_SendsSelectedPropertiesWithoutDefaults()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "app", "list", "--select", "id", "appId"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("id,appId", Assert.Single(_mockWire.SentRequests()).QueryValue("$select"));
    }

    [Fact]
    public async Task RunAsync_AppListOrderbyTwoValues_JoinsWithComma()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "app", "list", "--orderby", "displayName", "createdDateTime desc"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("displayName,createdDateTime desc", Assert.Single(_mockWire.SentRequests()).QueryValue("$orderby"));
    }

    [Fact]
    public async Task RunAsync_AppListSearch_SendsSearchAsTyped()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "app", "list", "--search", "\"displayName:contoso\""]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("\"displayName:contoso\"", Assert.Single(_mockWire.SentRequests()).QueryValue("$search"));
    }

    // ── CLI argument binding ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GroupShowShortAlias_BindsGroup()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "show", "-g", GroupId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET /v1.0/groups/{GroupId}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_SingleValueFlagRepeated_UsesLastValue()
    {
        _mockWire.RespondTo(HttpMethod.Get, $"{UsersUrl}/second", HttpStatusCode.OK, """{"id":"second"}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "user", "show", "--id", "first", "--id", "second"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(["GET /v1.0/users/second"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_MultiValueFlagRepeated_SendsSameSelectAsSpaceSeparatedValues()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["msgraph", "user", "show", "--id", UserId, "--select", "jobTitle", "--select", "mail"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("jobTitle,mail", Assert.Single(_mockWire.SentRequests()).QueryValue("$select"));
    }

    [Fact]
    public async Task RunAsync_BareAppendSelectedBeforeAnotherFlag_TreatedAsTrue()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["msgraph", "group", "list", "--append-selected", "--select", "visibility"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal($"{DefaultGroupProperties},visibility", Assert.Single(_mockWire.SentRequests()).QueryValue("$select"));
    }
}
