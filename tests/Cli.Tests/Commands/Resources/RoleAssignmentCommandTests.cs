// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Moq;
using ResourcesIndex = Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex;

namespace Cli.Tests.Commands.Resources;

/// <summary>
/// Runs role assignment CLI commands through the real engine and checks the error for each rejected input. The wire
/// mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
public sealed class RoleAssignmentCommandTests : IDisposable
{
    private const string ExtensionName = "Resources";
    private const string RoleId = "acdd72a7-3385-48ef-bd42-f606fba81ae7";
    private const string PrincipalId = "dddddddd-0000-0000-0000-000000000004";
    private const string AssignmentName = "eeeeeeee-0000-0000-0000-000000000005";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Engine _engine;

    public RoleAssignmentCommandTests() => _engine = CommandTestHelpers.CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);

    public void Dispose() => _engine.Dispose();

    // ── role assignment scope (shared by the role assignment commands) ─────────────────────────

    [Theory]
    [InlineData("role assignment list", new string[0])]
    [InlineData("role assignment create", new[] { "--role", RoleId, "--assignee", PrincipalId })]
    [InlineData("role assignment delete", new[] { "--name", AssignmentName })]
    public async Task RunAsync_RoleAssignmentScopeAndResourceGroup_ThrowsUsageError(string command, string[] args)
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, command, [.. args, "--scope", "/subscriptions/sub/resourceGroups/rg", "--resource-group", "rg"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Specify either a scope or a resource group, not both.", Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentBlankScope_ThrowsUsageError()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "role assignment list", ["--scope", " "], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Scope must not be empty.", Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentBlankResourceGroup_ThrowsUsageError()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "role assignment list", ["--resource-group", ""], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Resource group must not be empty.", Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentScopeWithMultipleLeadingSlashes_ThrowsUsageError()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "role assignment list", ["--scope", "//subscriptions/sub"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "The scope '//subscriptions/sub' must not start with multiple slashes.",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    // A blank --subscription-id replaces the profile's subscription, leaving nothing to build a scope from.
    [Fact]
    public async Task RunAsync_RoleAssignmentBlankSubscriptionWithoutScope_ThrowsMissingRequiredArgument()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "role assignment list", ["--subscription-id", ""], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "A scope is required. Specify a scope or a resource group, or set an active subscription.",
            Assert.IsType<MissingRequiredArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentBlankSubscriptionWithResourceGroup_ThrowsMissingRequiredArgument()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, "role assignment list", ["--subscription-id", "", "--resource-group", "rg"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "A subscription is required to resolve a resource group scope. Specify a subscription, or set an active subscription.",
            Assert.IsType<MissingRequiredArgumentException>(exception).Message);
    }

    // ── role assignment assignee (shared by list and create) ───────────────────────────────────

    [Theory]
    [InlineData("role assignment list", new string[0])]
    [InlineData("role assignment create", new[] { "--role", RoleId })]
    public async Task RunAsync_RoleAssignmentAssigneeAndAssigneeObjectId_ThrowsUsageError(string command, string[] args)
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, command, [.. args, "--assignee", PrincipalId, "--assignee-object-id", PrincipalId], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Specify only one assignee.", Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Theory]
    [InlineData("role assignment list", new string[0])]
    [InlineData("role assignment create", new[] { "--role", RoleId })]
    public async Task RunAsync_RoleAssignmentAssigneeNotObjectId_ThrowsUsageError(string command, string[] args)
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, command, [.. args, "--assignee", "user@contoso.com"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "The assignee 'user@contoso.com' is not an object ID (GUID). Name, UPN, and service-principal resolution is not yet supported; " +
            "specify the principal's object ID (GUID).",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Theory]
    [InlineData("role assignment list", new string[0])]
    [InlineData("role assignment create", new[] { "--role", RoleId })]
    public async Task RunAsync_RoleAssignmentAssigneeObjectIdNotGuid_ThrowsUsageError(string command, string[] args)
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, command, [.. args, "--assignee-object-id", "not-a-guid"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("The assignee object ID 'not-a-guid' is not a valid object ID (GUID).", Assert.IsType<UsageErrorException>(exception).Message);
    }

    // ── role assignment name (shared by create and delete) ─────────────────────────────────────

    [Theory]
    [InlineData("role assignment create", new[] { "--role", RoleId, "--assignee", PrincipalId })]
    [InlineData("role assignment delete", new string[0])]
    public async Task RunAsync_RoleAssignmentNameNotGuid_ThrowsUsageError(string command, string[] args)
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, command, [.. args, "--name", "not-a-guid"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("The role assignment name 'not-a-guid' must be a GUID.", Assert.IsType<UsageErrorException>(exception).Message);
    }

    // ── role assignment list ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_RoleAssignmentListRoleName_ThrowsUsageError()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "role assignment list", ["--role", "Reader"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "The role 'Reader' is not a role definition ID (GUID). Role name resolution is not yet supported; specify the role definition ID (GUID).",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentListIncludeGroupsWithoutAssignee_ThrowsUsageError()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "role assignment list", ["--include-groups"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Expanding group memberships requires an assignee. Specify the principal's object ID (GUID).",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentListAllWithIncludeInherited_ThrowsUsageError()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "role assignment list", ["--all", "--include-inherited"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "Listing every nested scope cannot be combined with including inherited (ancestor) assignments; they widen in opposite directions.",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentListAllWithResourceGroup_ThrowsUsageError()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "role assignment list", ["--all", "--resource-group", "rg"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "--all lists role assignments across the whole subscription and cannot be combined with a scope or resource group.",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentListAllWithBlankSubscription_ThrowsMissingRequiredArgument()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "role assignment list", ["--all", "--subscription-id", ""], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "A subscription is required to list all role assignments. Specify a subscription, or set an active subscription.",
            Assert.IsType<MissingRequiredArgumentException>(exception).Message);
    }

    // ── role assignment create ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_RoleAssignmentCreateWithoutAssignee_ThrowsMissingRequiredArgument()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "role assignment create", ["--role", RoleId], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "A principal is required. Specify the principal's object ID (GUID) to assign the role to.",
            Assert.IsType<MissingRequiredArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentCreateBlankRole_ThrowsMissingRequiredArgument()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "role assignment create", ["--role", "", "--assignee", PrincipalId], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "A role is required. Specify the role definition ID (GUID).",
            Assert.IsType<MissingRequiredArgumentException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentCreateRoleName_ThrowsUsageError()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "role assignment create", ["--role", "Reader", "--assignee", PrincipalId], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "The role 'Reader' is not a role definition ID. Role name resolution is not yet supported; specify a role definition GUID, " +
            "or a fully-qualified role definition ID ending in '/providers/Microsoft.Authorization/roleDefinitions/{guid}'.",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentCreateInvalidRoleDefinitionId_ThrowsUsageError()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, "role assignment create", ["--role", "/subscriptions/sub/roleDefinitions/reader", "--assignee", PrincipalId], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "The role '/subscriptions/sub/roleDefinitions/reader' is not a valid role definition ID. Specify a role definition GUID, " +
            "or a fully-qualified role definition ID ending in '/providers/Microsoft.Authorization/roleDefinitions/{guid}'.",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentCreateBlankCondition_ThrowsUsageError()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, "role assignment create", ["--role", RoleId, "--assignee", PrincipalId, "--condition", ""], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal("Condition must not be empty.", Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentCreateConditionVersionWithoutCondition_ThrowsUsageError()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName, "role assignment create", ["--role", RoleId, "--assignee", PrincipalId, "--condition-version", "2.0"], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "A condition version was specified without a condition. Specify a condition, or omit the condition version.",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    [Fact]
    public async Task RunAsync_RoleAssignmentCreateUnsupportedConditionVersion_ThrowsUsageError()
    {
        using ISession session = _engine.OpenCliSession(
            ExtensionName,
            "role assignment create",
            ["--role", RoleId, "--assignee", PrincipalId, "--condition", "@Resource[name] StringEquals 'x'", "--condition-version", "1.0"],
            _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "The condition version '1.0' is not supported. The only supported condition version is '2.0'.",
            Assert.IsType<UsageErrorException>(exception).Message);
    }

    // ── role assignment delete ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_RoleAssignmentDeleteBlankName_ThrowsMissingRequiredArgument()
    {
        using ISession session = _engine.OpenCliSession(ExtensionName, "role assignment delete", ["--name", ""], _output);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            "A role assignment name is required. Specify the role assignment name (GUID) to delete.",
            Assert.IsType<MissingRequiredArgumentException>(exception).Message);
    }
}
