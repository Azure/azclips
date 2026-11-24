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
/// Runs role assignment CLI commands through the real engine against canned ARM responses and checks what each command
/// writes, or the error it throws for a response.
/// </summary>
public sealed class RoleAssignmentCommandResponseTests : IDisposable
{
    private const string ExtensionName = "Resources";
    private const string PrincipalId = "aaaaaaaa-0000-0000-0000-000000000001";
    private const string OtherPrincipalId = "cccccccc-0000-0000-0000-000000000003";
    private const string AssignmentName = "bbbbbbbb-0000-0000-0000-000000000002";
    private const string FirstAssignmentName = "11111111-0000-0000-0000-000000000001";
    private const string SecondAssignmentName = "22222222-0000-0000-0000-000000000002";
    private const string ThirdAssignmentName = "33333333-0000-0000-0000-000000000003";
    private const string FourthAssignmentName = "44444444-0000-0000-0000-000000000004";
    private const string RoleId = "acdd72a7-3385-48ef-bd42-f606fba81ae7";
    private const string OtherRoleId = "b24988ac-6180-42a0-ab88-20f7382dd24c";
    private const string GroupName = "rg";
    private const string ApiVersion = "2022-04-01";
    private const string SubscriptionScope = $"/subscriptions/{SubscriptionId}";
    private const string GroupScope = $"{SubscriptionScope}/resourceGroups/{GroupName}";
    private const string AssignmentsUrl = $"{ArmUrl}{SubscriptionScope}/providers/Microsoft.Authorization/roleAssignments";
    private const string AssignmentUrl = $"{AssignmentsUrl}/{AssignmentName}";
    private const string ConflictError = """{"error":{"code":"RoleAssignmentExists","message":"exists"}}""";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public RoleAssignmentCommandResponseTests() => _engine = CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // ── role assignment create ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_RoleAssignmentCreate_WritesCreatedAssignmentName()
    {
        _mockWire.RespondTo(HttpMethod.Put, AssignmentUrl, HttpStatusCode.Created, Assignment(AssignmentName, PrincipalId, RoleId, SubscriptionScope));
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["role", "assignment", "create", "--assignee-object-id", PrincipalId, "--role", RoleId, "--name", AssignmentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(AssignmentName, Assert.IsType<RoleAssignment>(Assert.Single(_output)).Name);
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentCreate_WritesAssignmentPrincipalType()
    {
        _mockWire.RespondTo(HttpMethod.Put, AssignmentUrl, HttpStatusCode.Created, Assignment(AssignmentName, PrincipalId, RoleId, SubscriptionScope));
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["role", "assignment", "create", "--assignee-object-id", PrincipalId, "--role", RoleId, "--name", AssignmentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("User", Assert.IsType<RoleAssignment>(Assert.Single(_output)).Properties?.PrincipalType);
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentCreateWithoutNameConflict_WritesExistingAssignment()
    {
        _mockWire.RespondToAnyUrl(HttpMethod.Put, HttpStatusCode.Conflict, ConflictError);
        _mockWire.RespondTo(HttpMethod.Get, AssignmentsUrl, HttpStatusCode.OK, $$"""{"value":[{{Assignment(AssignmentName, PrincipalId, RoleId, SubscriptionScope)}}]}""");
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["role", "assignment", "create", "--assignee-object-id", PrincipalId, "--role", RoleId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(AssignmentName, Assert.IsType<RoleAssignment>(Assert.Single(_output)).Name);
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentCreateNamedConflict_ThrowsStatusUrlAndBody()
    {
        _mockWire.RespondTo(HttpMethod.Put, AssignmentUrl, HttpStatusCode.Conflict, ConflictError);
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["role", "assignment", "create", "--assignee-object-id", PrincipalId, "--role", RoleId, "--name", AssignmentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            $$$"""409 Conflict from {{{AssignmentUrl}}}?api-version={{{ApiVersion}}}: {"error":{"code":"RoleAssignmentExists","message":"exists"}}""",
            Assert.IsType<HttpRequestException>(exception).Message);
    }

    // ── role assignment delete ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_RoleAssignmentDeleteNoContent_WritesNothing()
    {
        _mockWire.RespondTo(HttpMethod.Delete, AssignmentUrl, HttpStatusCode.NoContent);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["role", "assignment", "delete", "--name", AssignmentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(_output);
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentDeleteOk_WritesDeletedAssignment()
    {
        _mockWire.RespondTo(HttpMethod.Delete, AssignmentUrl, HttpStatusCode.OK, Assignment(AssignmentName, PrincipalId, RoleId, SubscriptionScope));
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["role", "assignment", "delete", "--name", AssignmentName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(AssignmentName, Assert.IsType<RoleAssignment>(Assert.Single(_output)).Name);
    }

    // ── role assignment list ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_RoleAssignmentList_WritesOnlyAssignmentsAtExactScope()
    {
        _mockWire.RespondTo(HttpMethod.Get, AssignmentsUrl, HttpStatusCode.OK, ListOfMixedAssignments());
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["role", "assignment", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([FirstAssignmentName, SecondAssignmentName], Assert.IsType<List<RoleAssignment>>(Assert.Single(_output)).Select(a => a.Name));
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentListIncludeInherited_WritesEveryReturnedAssignment()
    {
        _mockWire.RespondTo(HttpMethod.Get, AssignmentsUrl, HttpStatusCode.OK, ListOfInheritedAssignments());
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["role", "assignment", "list", "--include-inherited"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            [FirstAssignmentName, SecondAssignmentName, FourthAssignmentName],
            Assert.IsType<List<RoleAssignment>>(Assert.Single(_output)).Select(a => a.Name));
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentListAssigneeAnyCase_WritesOnlyAssigneeAssignments()
    {
        _mockWire.RespondTo(HttpMethod.Get, AssignmentsUrl, HttpStatusCode.OK, ListOfMixedAssignments());
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["role", "assignment", "list", "--assignee", PrincipalId.ToUpperInvariant()]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([FirstAssignmentName], Assert.IsType<List<RoleAssignment>>(Assert.Single(_output)).Select(a => a.Name));
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentListRoleAnyCase_WritesOnlyRoleAssignments()
    {
        _mockWire.RespondTo(HttpMethod.Get, AssignmentsUrl, HttpStatusCode.OK, ListOfMixedAssignments());
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["role", "assignment", "list", "--role", OtherRoleId.ToUpperInvariant()]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([SecondAssignmentName], Assert.IsType<List<RoleAssignment>>(Assert.Single(_output)).Select(a => a.Name));
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentListAll_WritesEveryAssignment()
    {
        _mockWire.RespondTo(HttpMethod.Get, AssignmentsUrl, HttpStatusCode.OK, ListOfMixedAssignments());
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["role", "assignment", "list", "--all"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            [FirstAssignmentName, SecondAssignmentName, ThirdAssignmentName, FourthAssignmentName],
            Assert.IsType<List<RoleAssignment>>(Assert.Single(_output)).Select(a => a.Name));
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentListNextLink_WritesAssignmentsFromEveryPage()
    {
        _mockWire.RespondInOrder(
            $$"""{"value":[{{Assignment(FirstAssignmentName, PrincipalId, RoleId, SubscriptionScope)}}],"nextLink":"{{AssignmentsUrl}}?api-version={{ApiVersion}}&$filter=atScope()&$skiptoken=p2"}""",
            $$"""{"value":[{{Assignment(SecondAssignmentName, PrincipalId, RoleId, SubscriptionScope)}}]}""");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["role", "assignment", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([FirstAssignmentName, SecondAssignmentName], Assert.IsType<List<RoleAssignment>>(Assert.Single(_output)).Select(a => a.Name));
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentListEmpty_WritesEmptyList()
    {
        _mockWire.RespondTo(HttpMethod.Get, AssignmentsUrl, HttpStatusCode.OK, EmptyList);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["role", "assignment", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(Assert.IsType<List<RoleAssignment>>(Assert.Single(_output)));
    }

    private string Assignment(string name, string principalId, string roleId, string scope) =>
        $$$"""{"id":"{{{scope.TrimEnd('/')}}}/providers/Microsoft.Authorization/roleAssignments/{{{name}}}","name":"{{{name}}}","type":"Microsoft.Authorization/roleAssignments","properties":{"roleDefinitionId":"{{{SubscriptionScope}}}/providers/Microsoft.Authorization/roleDefinitions/{{{roleId}}}","principalId":"{{{principalId}}}","principalType":"User","scope":"{{{scope}}}"}}""";

    // Two assignments at the subscription (different principal and role), one at a child resource group and one at the root.
    private string ListOfMixedAssignments() =>
        "{\"value\":["
        + Assignment(FirstAssignmentName, PrincipalId, RoleId, SubscriptionScope) + ","
        + Assignment(SecondAssignmentName, OtherPrincipalId, OtherRoleId, SubscriptionScope) + ","
        + Assignment(ThirdAssignmentName, PrincipalId, RoleId, GroupScope) + ","
        + Assignment(FourthAssignmentName, PrincipalId, RoleId, "/")
        + "]}";

    // Two assignments at the subscription and one at the root: the subscription and its ancestors, as atScope() returns.
    private string ListOfInheritedAssignments() =>
        "{\"value\":["
        + Assignment(FirstAssignmentName, PrincipalId, RoleId, SubscriptionScope) + ","
        + Assignment(SecondAssignmentName, OtherPrincipalId, OtherRoleId, SubscriptionScope) + ","
        + Assignment(FourthAssignmentName, PrincipalId, RoleId, "/")
        + "]}";
}
