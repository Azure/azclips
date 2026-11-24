// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using ResourcesIndex = Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex;

namespace Cli.Tests.Commands.Resources;

/// <summary>
/// Runs role assignment CLI commands through the real engine and checks the request each one sends to ARM: method,
/// path, decoded query values and JSON body. The wire mock is strict, so an unexpected request fails the test.
/// </summary>
public sealed class RoleAssignmentCommandRequestTests : IDisposable
{
    private const string ExtensionName = "Resources";
    private const string PrincipalId = "aaaaaaaa-0000-0000-0000-000000000001";
    private const string AssignmentName = "bbbbbbbb-0000-0000-0000-000000000002";
    private const string RoleId = "acdd72a7-3385-48ef-bd42-f606fba81ae7";
    private const string GroupName = "rg";
    private const string ApiVersion = "2022-04-01";
    private const string SubscriptionScope = $"/subscriptions/{SubscriptionId}";
    private const string GroupScope = $"{SubscriptionScope}/resourceGroups/{GroupName}";
    private const string RoleDefinitionId = $"{SubscriptionScope}/providers/Microsoft.Authorization/roleDefinitions/{RoleId}";
    private const string AssignmentsPath = $"{SubscriptionScope}/providers/Microsoft.Authorization/roleAssignments";
    private const string GroupAssignmentsPath = $"{GroupScope}/providers/Microsoft.Authorization/roleAssignments";
    private const string AssignmentPath = $"{AssignmentsPath}/{AssignmentName}";
    private const string GroupAssignmentPath = $"{GroupAssignmentsPath}/{AssignmentName}";
    private const string AssignmentsUrl = ArmUrl + AssignmentsPath;
    private const string GroupAssignmentsUrl = ArmUrl + GroupAssignmentsPath;
    private const string AssignmentUrl = ArmUrl + AssignmentPath;
    private const string ConflictError = """{"error":{"code":"RoleAssignmentExists","message":"exists"}}""";
    private const string AssignmentBody = $$$"""{"id":"{{{AssignmentPath}}}","name":"{{{AssignmentName}}}","type":"Microsoft.Authorization/roleAssignments","properties":{"roleDefinitionId":"{{{RoleDefinitionId}}}","principalId":"{{{PrincipalId}}}","principalType":"User","scope":"{{{SubscriptionScope}}}"}}""";
    private const string GroupAssignmentBody = $$$"""{"id":"{{{GroupAssignmentPath}}}","name":"{{{AssignmentName}}}","type":"Microsoft.Authorization/roleAssignments","properties":{"roleDefinitionId":"{{{RoleDefinitionId}}}","principalId":"{{{PrincipalId}}}","principalType":"User","scope":"{{{GroupScope}}}"}}""";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public RoleAssignmentCommandRequestTests() => _engine = CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // Each command with the arguments it needs, the method it sends, the URL it sends to and a response for it.
    public static TheoryData<string[], string, string, string?> GetRoleAssignmentCommands() => new()
    {
        { ["role", "assignment", "create", "--assignee-object-id", PrincipalId, "--role", RoleId, "--name", AssignmentName], "PUT", AssignmentUrl, AssignmentBody },
        { ["role", "assignment", "delete", "--name", AssignmentName], "DELETE", AssignmentUrl, null },
        { ["role", "assignment", "list"], "GET", AssignmentsUrl, EmptyList },
    };

    // Each list command with the $filter it sends.
    public static TheoryData<string[], string> GetRoleAssignmentListFilters() => new()
    {
        { ["role", "assignment", "list"], "atScope()" },
        { ["role", "assignment", "list", "--include-inherited"], "atScope()" },
        { ["role", "assignment", "list", "--assignee", PrincipalId], "atScope()" },
        { ["role", "assignment", "list", "--assignee-object-id", PrincipalId, "--include-groups"], $"atScope() and assignedTo('{PrincipalId}')" },
        { ["role", "assignment", "list", "--all", "--assignee-object-id", PrincipalId], $"principalId eq '{PrincipalId}'" },
        { ["role", "assignment", "list", "--all", "--assignee-object-id", PrincipalId, "--include-groups"], $"assignedTo('{PrincipalId}')" },
    };

    [Theory]
    [MemberData(nameof(GetRoleAssignmentCommands))]
    public async Task RunAsync_RoleAssignmentCommand_SendsAuthorizationApiVersion(string[] commandLine, string method, string url, string? responseBody)
    {
        _mockWire.RespondTo(new HttpMethod(method), url, HttpStatusCode.OK, responseBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(ApiVersion, Assert.Single(_mockWire.SentRequests()).QueryValue("api-version"));
    }

    [Theory]
    [MemberData(nameof(GetRoleAssignmentCommands))]
    public async Task RunAsync_RoleAssignmentCommandResourceGroup_SendsRequestUnderGroupScope(string[] commandLine, string method, string url, string? responseBody)
    {
        string groupUrl = url.Replace(AssignmentsUrl, GroupAssignmentsUrl);
        _mockWire.RespondTo(new HttpMethod(method), groupUrl, HttpStatusCode.OK, responseBody == AssignmentBody ? GroupAssignmentBody : responseBody);
        CommandInvocation invocation = CreateInvocation(ExtensionName, [.. commandLine, "-g", GroupName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"{method} {new Uri(groupUrl).AbsolutePath}"], _mockWire.RequestLines());
    }

    // ── role assignment create ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_RoleAssignmentCreateName_SendsPutToAssignmentPath()
    {
        _mockWire.RespondTo(HttpMethod.Put, AssignmentUrl, HttpStatusCode.Created, AssignmentBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["role", "assignment", "create", "--assignee-object-id", PrincipalId, "--role", RoleId, "--name", AssignmentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"PUT {AssignmentPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentCreateRequiredArgs_SendsRoleDefinitionAndPrincipalBody()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, AssignmentUrl, HttpStatusCode.Created, AssignmentBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["role", "assignment", "create", "--assignee-object-id", PrincipalId, "--role", RoleId, "--name", AssignmentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            """{"properties":{"roleDefinitionId":"/subscriptions/00000000-0000-0000-0000-000000000000/providers/Microsoft.Authorization/roleDefinitions/acdd72a7-3385-48ef-bd42-f606fba81ae7","principalId":"aaaaaaaa-0000-0000-0000-000000000001"}}""",
            Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentCreateAllOptions_SendsEveryPropertyInBody()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, AssignmentUrl, HttpStatusCode.Created, AssignmentBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "role", "assignment", "create",
                "--scope", SubscriptionScope,
                "--assignee-object-id", PrincipalId,
                "--role", RoleId,
                "--assignee-principal-type", "User",
                "--name", AssignmentName,
                "--description", "desc",
                "--condition", "@x",
                "--condition-version", "2.0",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            """{"properties":{"roleDefinitionId":"/subscriptions/00000000-0000-0000-0000-000000000000/providers/Microsoft.Authorization/roleDefinitions/acdd72a7-3385-48ef-bd42-f606fba81ae7","principalId":"aaaaaaaa-0000-0000-0000-000000000001","principalType":"User","description":"desc","condition":"@x","conditionVersion":"2.0"}}""",
            Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentCreateConditionWithoutVersion_SendsConditionVersion2()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, AssignmentUrl, HttpStatusCode.Created, AssignmentBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["role", "assignment", "create", "--assignee-object-id", PrincipalId, "--role", RoleId, "--name", AssignmentName, "--condition", "@x"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            """{"properties":{"roleDefinitionId":"/subscriptions/00000000-0000-0000-0000-000000000000/providers/Microsoft.Authorization/roleDefinitions/acdd72a7-3385-48ef-bd42-f606fba81ae7","principalId":"aaaaaaaa-0000-0000-0000-000000000001","condition":"@x","conditionVersion":"2.0"}}""",
            Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentCreateAssignee_SendsAssigneeAsPrincipalId()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, AssignmentUrl, HttpStatusCode.Created, AssignmentBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["role", "assignment", "create", "--assignee", PrincipalId, "--role", RoleId, "--name", AssignmentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            """{"properties":{"roleDefinitionId":"/subscriptions/00000000-0000-0000-0000-000000000000/providers/Microsoft.Authorization/roleDefinitions/acdd72a7-3385-48ef-bd42-f606fba81ae7","principalId":"aaaaaaaa-0000-0000-0000-000000000001"}}""",
            Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentCreateRoleDefinitionId_SendsRoleDefinitionIdAsGiven()
    {
        List<string?> sentBodies = _mockWire.RespondTo(HttpMethod.Put, AssignmentUrl, HttpStatusCode.Created, AssignmentBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["role", "assignment", "create", "--assignee-object-id", PrincipalId, "--role", RoleDefinitionId, "--name", AssignmentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            """{"properties":{"roleDefinitionId":"/subscriptions/00000000-0000-0000-0000-000000000000/providers/Microsoft.Authorization/roleDefinitions/acdd72a7-3385-48ef-bd42-f606fba81ae7","principalId":"aaaaaaaa-0000-0000-0000-000000000001"}}""",
            Assert.Single(sentBodies));
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentCreateScopeWithoutLeadingSlash_SendsPutUnderNormalisedScope()
    {
        _mockWire.RespondTo(HttpMethod.Put, ArmUrl + GroupAssignmentPath, HttpStatusCode.Created, GroupAssignmentBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "role", "assignment", "create",
                "--scope", $"subscriptions/{SubscriptionId}/resourceGroups/{GroupName}/",
                "--assignee-object-id", PrincipalId,
                "--role", RoleId,
                "--name", AssignmentName,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"PUT {GroupAssignmentPath}"], _mockWire.RequestLines());
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentCreateWithoutName_SendsPutToGeneratedGuidName()
    {
        _mockWire.RespondToAnyUrl(HttpMethod.Put, HttpStatusCode.Created, AssignmentBody);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["role", "assignment", "create", "--assignee-object-id", PrincipalId, "--role", RoleId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.True(Guid.TryParse(Assert.Single(_mockWire.SentRequests()).RequestUri!.Segments[^1], out _));
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentCreateWithoutNameConflict_SendsLookupByPrincipal()
    {
        _mockWire.RespondToAnyUrl(HttpMethod.Put, HttpStatusCode.Conflict, ConflictError);
        _mockWire.RespondTo(HttpMethod.Get, AssignmentsUrl, HttpStatusCode.OK, $$"""{"value":[{{AssignmentBody}}]}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["role", "assignment", "create", "--assignee-object-id", PrincipalId, "--role", RoleId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal($"principalId eq '{PrincipalId}'", _mockWire.SentRequests()[1].QueryValue("$filter"));
    }

    // ── role assignment delete ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_RoleAssignmentDelete_SendsDeleteToAssignmentPath()
    {
        _mockWire.RespondTo(HttpMethod.Delete, AssignmentUrl, HttpStatusCode.NoContent);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["role", "assignment", "delete", "--name", AssignmentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"DELETE {AssignmentPath}?api-version={ApiVersion}"], _mockWire.FullRequestLines());
    }

    // ── role assignment list ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_RoleAssignmentList_SendsGetToSubscriptionAssignments()
    {
        _mockWire.RespondTo(HttpMethod.Get, AssignmentsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["role", "assignment", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {AssignmentsPath}"], _mockWire.RequestLines());
    }

    [Theory]
    [MemberData(nameof(GetRoleAssignmentListFilters))]
    public async Task RunAsync_RoleAssignmentListOptions_SendsFilter(string[] commandLine, string expectedFilter)
    {
        _mockWire.RespondTo(HttpMethod.Get, AssignmentsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, commandLine);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(expectedFilter, Assert.Single(_mockWire.SentRequests()).QueryValue("$filter"));
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentListAll_SendsNoFilter()
    {
        _mockWire.RespondTo(HttpMethod.Get, AssignmentsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["role", "assignment", "list", "--all"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Null(Assert.Single(_mockWire.SentRequests()).QueryValue("$filter"));
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentListScope_SendsGetToScopeAssignments()
    {
        _mockWire.RespondTo(HttpMethod.Get, GroupAssignmentsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["role", "assignment", "list", "--scope", GroupScope]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([$"GET {GroupAssignmentsPath}"], _mockWire.RequestLines());
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentListNextLink_SendsNextLinkSkipToken()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[{{AssignmentBody}}],"nextLink":"{{AssignmentsUrl}}?api-version={{ApiVersion}}&$filter=atScope()&$skiptoken=p2"}""",
            EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["role", "assignment", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("p2", _mockWire.SentRequests()[1].QueryValue("$skiptoken"));
    }
}
