// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Engine;
using Microsoft.Azure.Azclips.Cmdlets.Resources;
using Moq;
using static Cmdlets.Tests.Commands.CommandTestHelpers;
using ResourcesIndex = Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex;

namespace Cmdlets.Tests.Commands.Resources;

/// <summary>
/// Runs role assignment cmdlets in PowerShell through the real engine and checks the error for each rejected input.
/// The wire mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class RoleAssignmentCommandTests : IDisposable
{
    private const string SubscriptionId = "11111111-1111-1111-1111-111111111111";
    private const string RoleId = "acdd72a7-3385-48ef-bd42-f606fba81ae7";
    private const string PrincipalId = "dddddddd-0000-0000-0000-000000000004";
    private const string AssignmentName = "eeeeeeee-0000-0000-0000-000000000005";
    private const string BlankScopeMessage = "Scope must not be empty.";
    private const string BlankResourceGroupMessage = "Resource group must not be empty.";
    private const string MultipleLeadingSlashesMessage = "The scope '//subscriptions/sub' must not start with multiple slashes.";
    private const string NoScopeMessage = "A scope is required. Specify a scope or a resource group, or set an active subscription.";
    private const string ObjectIdNotGuidMessage = "The assignee object ID 'not-a-guid' is not a valid object ID (GUID).";
    private const string NameNotGuidMessage = "Cannot validate argument on parameter 'Name'. The role assignment name 'not-a-guid' must be a GUID.";
    private const string ParameterSetMessage = "Parameter set cannot be resolved using the specified named parameters. One or more parameters issued cannot be used together or an insufficient number of parameters were provided.";

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly Engine _engine;

    public RoleAssignmentCommandTests()
    {
        _engine = CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);
        RegisterEngine(_engine);
    }

    public void Dispose()
    {
        AzclipsServiceProvider.Dispose();
        _engine.Dispose();
    }

    // ── Get-AzRoleAssignment ───────────────────────────────────────────────────────────────────
    // A whitespace SubscriptionId replaces the profile's subscription, so there is no subscription to fall back to.

    [Fact]
    public void Invoke_GetRoleAssignmentWhitespaceScope_ThrowsUsageError()
    {
        string whitespaceScope = " ";
        using PowerShell shell = CreateShell<GetRoleAssignmentCmdlet>();
        shell.AddParameter("Scope", whitespaceScope);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            BlankScopeMessage,
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_GetRoleAssignmentWhitespaceResourceGroupName_ThrowsUsageError()
    {
        string whitespaceResourceGroupName = " ";
        using PowerShell shell = CreateShell<GetRoleAssignmentCmdlet>();
        shell.AddParameter("ResourceGroupName", whitespaceResourceGroupName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            BlankResourceGroupMessage,
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_GetRoleAssignmentScopeWithMultipleLeadingSlashes_ThrowsUsageError()
    {
        string scopeWithMultipleLeadingSlashes = "//subscriptions/sub";
        using PowerShell shell = CreateShell<GetRoleAssignmentCmdlet>();
        shell.AddParameter("Scope", scopeWithMultipleLeadingSlashes);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            MultipleLeadingSlashesMessage,
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_GetRoleAssignmentWhitespaceSubscriptionId_ThrowsMissingRequiredArgument()
    {
        string whitespaceSubscriptionId = " ";
        using PowerShell shell = CreateShell<GetRoleAssignmentCmdlet>();
        shell.AddParameter("SubscriptionId", whitespaceSubscriptionId);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            NoScopeMessage,
            Assert.IsType<MissingRequiredArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_GetRoleAssignmentWhitespaceSubscriptionIdWithResourceGroupName_ThrowsMissingRequiredArgument()
    {
        string whitespaceSubscriptionId = " ";
        using PowerShell shell = CreateShell<GetRoleAssignmentCmdlet>();
        shell.AddParameter("SubscriptionId", whitespaceSubscriptionId).AddParameter("ResourceGroupName", "rg");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "A subscription is required to resolve a resource group scope. Specify a subscription, or set an active subscription.",
            Assert.IsType<MissingRequiredArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_GetRoleAssignmentObjectIdNotGuid_ThrowsUsageError()
    {
        string objectIdNotGuid = "not-a-guid";
        using PowerShell shell = CreateShell<GetRoleAssignmentCmdlet>();
        shell.AddParameter("SubscriptionId", SubscriptionId).AddParameter("ObjectId", objectIdNotGuid);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            ObjectIdNotGuidMessage,
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_GetRoleAssignmentRoleDefinitionIdRoleName_ThrowsUsageError()
    {
        string roleName = "Reader";
        using PowerShell shell = CreateShell<GetRoleAssignmentCmdlet>();
        shell.AddParameter("SubscriptionId", SubscriptionId).AddParameter("RoleDefinitionId", roleName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "The role 'Reader' is not a role definition ID (GUID). Role name resolution is not yet supported; specify the role definition ID (GUID).",
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_GetRoleAssignmentExpandPrincipalGroupsWithoutObjectId_ThrowsUsageError()
    {
        SwitchParameter expandPrincipalGroups = new(true);
        using PowerShell shell = CreateShell<GetRoleAssignmentCmdlet>();
        shell.AddParameter("SubscriptionId", SubscriptionId)
            .AddParameter("ExpandPrincipalGroups", expandPrincipalGroups);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Expanding group memberships requires an assignee. Specify the principal's object ID (GUID).",
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_GetRoleAssignmentIncludeNestedScopesWithIncludeInherited_ThrowsUsageError()
    {
        SwitchParameter includeInherited = new(true);
        using PowerShell shell = CreateShell<GetRoleAssignmentCmdlet>();
        shell.AddParameter("SubscriptionId", SubscriptionId)
            .AddParameter("IncludeNestedScopes", true)
            .AddParameter("IncludeInherited", includeInherited);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Listing every nested scope cannot be combined with including inherited (ancestor) assignments; they widen in opposite directions.",
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_GetRoleAssignmentIncludeNestedScopesWhitespaceSubscriptionId_ThrowsMissingRequiredArgument()
    {
        string whitespaceSubscriptionId = " ";
        using PowerShell shell = CreateShell<GetRoleAssignmentCmdlet>();
        shell.AddParameter("SubscriptionId", whitespaceSubscriptionId).AddParameter("IncludeNestedScopes", true);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "A subscription is required to list all role assignments. Specify a subscription, or set an active subscription.",
            Assert.IsType<MissingRequiredArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // Scope and ResourceGroupName are in different parameter sets, so PowerShell rejects both together before the
    // extension's "not both" check.
    [Fact]
    public void Invoke_GetRoleAssignmentScopeAndResourceGroupName_ThrowsParameterBindingException()
    {
        string resourceGroupName = "rg";
        using PowerShell shell = CreateShell<GetRoleAssignmentCmdlet>();
        shell.AddParameter("Scope", "/subscriptions/sub/resourceGroups/rg")
            .AddParameter("ResourceGroupName", resourceGroupName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(ParameterSetMessage, Assert.IsType<ParameterBindingException>(exception).Message);
    }

    // IncludeNestedScopes is only in the SubscriptionScope set, so PowerShell rejects it with ResourceGroupName before
    // the extension's own check.
    [Fact]
    public void Invoke_GetRoleAssignmentIncludeNestedScopesWithResourceGroupName_ThrowsParameterBindingException()
    {
        string resourceGroupName = "rg";
        using PowerShell shell = CreateShell<GetRoleAssignmentCmdlet>();
        shell.AddParameter("IncludeNestedScopes", true).AddParameter("ResourceGroupName", resourceGroupName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(ParameterSetMessage, Assert.IsType<ParameterBindingException>(exception).Message);
    }

    // ── New-AzRoleAssignment ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Invoke_NewRoleAssignmentWhitespaceScope_ThrowsUsageError()
    {
        string whitespaceScope = " ";
        using PowerShell shell = CreateShell<NewRoleAssignmentCmdlet>();
        shell.AddParameter("ObjectId", PrincipalId)
            .AddParameter("RoleDefinitionId", RoleId)
            .AddParameter("Scope", whitespaceScope);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            BlankScopeMessage,
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewRoleAssignmentWhitespaceResourceGroupName_ThrowsUsageError()
    {
        string whitespaceResourceGroupName = " ";
        using PowerShell shell = CreateShell<NewRoleAssignmentCmdlet>();
        shell.AddParameter("ObjectId", PrincipalId)
            .AddParameter("RoleDefinitionId", RoleId)
            .AddParameter("ResourceGroupName", whitespaceResourceGroupName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            BlankResourceGroupMessage,
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewRoleAssignmentScopeWithMultipleLeadingSlashes_ThrowsUsageError()
    {
        string scopeWithMultipleLeadingSlashes = "//subscriptions/sub";
        using PowerShell shell = CreateShell<NewRoleAssignmentCmdlet>();
        shell.AddParameter("ObjectId", PrincipalId)
            .AddParameter("RoleDefinitionId", RoleId)
            .AddParameter("Scope", scopeWithMultipleLeadingSlashes);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            MultipleLeadingSlashesMessage,
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewRoleAssignmentWhitespaceSubscriptionId_ThrowsMissingRequiredArgument()
    {
        string whitespaceSubscriptionId = " ";
        using PowerShell shell = CreateShell<NewRoleAssignmentCmdlet>();
        shell.AddParameter("ObjectId", PrincipalId)
            .AddParameter("RoleDefinitionId", RoleId)
            .AddParameter("SubscriptionId", whitespaceSubscriptionId);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            NoScopeMessage,
            Assert.IsType<MissingRequiredArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewRoleAssignmentObjectIdNotGuid_ThrowsUsageError()
    {
        string objectIdNotGuid = "not-a-guid";
        using PowerShell shell = CreateShell<NewRoleAssignmentCmdlet>();
        shell.AddParameter("SubscriptionId", SubscriptionId)
            .AddParameter("ObjectId", objectIdNotGuid)
            .AddParameter("RoleDefinitionId", RoleId);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            ObjectIdNotGuidMessage,
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // PowerShell rejects an empty value for a mandatory string while binding, so only whitespace reaches the extension.
    [Fact]
    public void Invoke_NewRoleAssignmentWhitespaceObjectId_ThrowsMissingRequiredArgument()
    {
        string whitespaceObjectId = " ";
        using PowerShell shell = CreateShell<NewRoleAssignmentCmdlet>();
        shell.AddParameter("SubscriptionId", SubscriptionId)
            .AddParameter("ObjectId", whitespaceObjectId)
            .AddParameter("RoleDefinitionId", RoleId);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "A principal is required. Specify the principal's object ID (GUID) to assign the role to.",
            Assert.IsType<MissingRequiredArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewRoleAssignmentWhitespaceRoleDefinitionId_ThrowsMissingRequiredArgument()
    {
        string whitespaceRoleDefinitionId = " ";
        using PowerShell shell = CreateShell<NewRoleAssignmentCmdlet>();
        shell.AddParameter("SubscriptionId", SubscriptionId)
            .AddParameter("ObjectId", PrincipalId)
            .AddParameter("RoleDefinitionId", whitespaceRoleDefinitionId);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "A role is required. Specify the role definition ID (GUID).",
            Assert.IsType<MissingRequiredArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewRoleAssignmentRoleDefinitionIdRoleName_ThrowsUsageError()
    {
        string roleName = "Reader";
        using PowerShell shell = CreateShell<NewRoleAssignmentCmdlet>();
        shell.AddParameter("SubscriptionId", SubscriptionId)
            .AddParameter("ObjectId", PrincipalId)
            .AddParameter("RoleDefinitionId", roleName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "The role 'Reader' is not a role definition ID. Role name resolution is not yet supported; specify a role definition GUID, " +
            "or a fully-qualified role definition ID ending in '/providers/Microsoft.Authorization/roleDefinitions/{guid}'.",
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewRoleAssignmentInvalidRoleDefinitionId_ThrowsUsageError()
    {
        string invalidRoleDefinitionId = "/subscriptions/sub/roleDefinitions/reader";
        using PowerShell shell = CreateShell<NewRoleAssignmentCmdlet>();
        shell.AddParameter("SubscriptionId", SubscriptionId)
            .AddParameter("ObjectId", PrincipalId)
            .AddParameter("RoleDefinitionId", invalidRoleDefinitionId);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "The role '/subscriptions/sub/roleDefinitions/reader' is not a valid role definition ID. Specify a role definition GUID, " +
            "or a fully-qualified role definition ID ending in '/providers/Microsoft.Authorization/roleDefinitions/{guid}'.",
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewRoleAssignmentWhitespaceCondition_ThrowsUsageError()
    {
        string whitespaceCondition = " ";
        using PowerShell shell = CreateShell<NewRoleAssignmentCmdlet>();
        shell.AddParameter("SubscriptionId", SubscriptionId)
            .AddParameter("ObjectId", PrincipalId)
            .AddParameter("RoleDefinitionId", RoleId)
            .AddParameter("Condition", whitespaceCondition);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Condition must not be empty.",
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewRoleAssignmentConditionVersionWithoutCondition_ThrowsUsageError()
    {
        string conditionVersion = "2.0";
        using PowerShell shell = CreateShell<NewRoleAssignmentCmdlet>();
        shell.AddParameter("SubscriptionId", SubscriptionId)
            .AddParameter("ObjectId", PrincipalId)
            .AddParameter("RoleDefinitionId", RoleId)
            .AddParameter("ConditionVersion", conditionVersion);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "A condition version was specified without a condition. Specify a condition, or omit the condition version.",
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_NewRoleAssignmentUnsupportedConditionVersion_ThrowsUsageError()
    {
        string unsupportedConditionVersion = "1.0";
        using PowerShell shell = CreateShell<NewRoleAssignmentCmdlet>();
        shell.AddParameter("SubscriptionId", SubscriptionId)
            .AddParameter("ObjectId", PrincipalId)
            .AddParameter("RoleDefinitionId", RoleId)
            .AddParameter("Condition", "@Resource[name] StringEquals 'x'")
            .AddParameter("ConditionVersion", unsupportedConditionVersion);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "The condition version '1.0' is not supported. The only supported condition version is '2.0'.",
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // [ValidateRoleAssignmentName] rejects the name while binding. The binder throws the internal
    // ParameterBindingValidationException, so the type is matched by its public base.
    [Fact]
    public void Invoke_NewRoleAssignmentNameNotGuid_ThrowsParameterBindingException()
    {
        string nameNotGuid = "not-a-guid";
        using PowerShell shell = CreateShell<NewRoleAssignmentCmdlet>();
        shell.AddParameter("SubscriptionId", SubscriptionId)
            .AddParameter("ObjectId", PrincipalId)
            .AddParameter("RoleDefinitionId", RoleId)
            .AddParameter("Name", nameNotGuid);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(NameNotGuidMessage, Assert.IsAssignableFrom<ParameterBindingException>(exception).Message);
    }

    // [ValidateSet] rejects the type while binding. The binder throws the internal ParameterBindingValidationException,
    // so the type is matched by its public base.
    [Fact]
    public void Invoke_NewRoleAssignmentObjectTypeNotInSet_ThrowsParameterBindingException()
    {
        string objectTypeNotInSet = "Application";
        using PowerShell shell = CreateShell<NewRoleAssignmentCmdlet>();
        shell.AddParameter("SubscriptionId", SubscriptionId)
            .AddParameter("ObjectId", PrincipalId)
            .AddParameter("RoleDefinitionId", RoleId)
            .AddParameter("ObjectType", objectTypeNotInSet);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Cannot validate argument on parameter 'ObjectType'. The argument \"Application\" does not belong to the set \"User,Group,ServicePrincipal,ForeignGroup\" specified by the ValidateSet attribute. Supply an argument that is in the set and then try the command again.",
            Assert.IsAssignableFrom<ParameterBindingException>(exception).Message);
    }

    // Scope and ResourceGroupName are in different parameter sets, so PowerShell rejects both together before the
    // extension's "not both" check.
    [Fact]
    public void Invoke_NewRoleAssignmentScopeAndResourceGroupName_ThrowsParameterBindingException()
    {
        string resourceGroupName = "rg";
        using PowerShell shell = CreateShell<NewRoleAssignmentCmdlet>();
        shell.AddParameter("ObjectId", PrincipalId)
            .AddParameter("RoleDefinitionId", RoleId)
            .AddParameter("Scope", "/subscriptions/sub/resourceGroups/rg")
            .AddParameter("ResourceGroupName", resourceGroupName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(ParameterSetMessage, Assert.IsType<ParameterBindingException>(exception).Message);
    }

    // ── Remove-AzRoleAssignment ────────────────────────────────────────────────────────────────
    // Remove-AzRoleAssignment is ConfirmImpact.High, so each test passes -Confirm:$false.

    [Fact]
    public void Invoke_RemoveRoleAssignmentWhitespaceScope_ThrowsUsageError()
    {
        string whitespaceScope = " ";
        using PowerShell shell = CreateShell<RemoveRoleAssignmentCmdlet>();
        shell.AddParameter("Name", AssignmentName)
            .AddParameter("Scope", whitespaceScope)
            .AddParameter("Confirm", false);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            BlankScopeMessage,
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_RemoveRoleAssignmentWhitespaceResourceGroupName_ThrowsUsageError()
    {
        string whitespaceResourceGroupName = " ";
        using PowerShell shell = CreateShell<RemoveRoleAssignmentCmdlet>();
        shell.AddParameter("Name", AssignmentName)
            .AddParameter("ResourceGroupName", whitespaceResourceGroupName)
            .AddParameter("Confirm", false);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            BlankResourceGroupMessage,
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_RemoveRoleAssignmentScopeWithMultipleLeadingSlashes_ThrowsUsageError()
    {
        string scopeWithMultipleLeadingSlashes = "//subscriptions/sub";
        using PowerShell shell = CreateShell<RemoveRoleAssignmentCmdlet>();
        shell.AddParameter("Name", AssignmentName)
            .AddParameter("Scope", scopeWithMultipleLeadingSlashes)
            .AddParameter("Confirm", false);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            MultipleLeadingSlashesMessage,
            Assert.IsType<UsageErrorException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_RemoveRoleAssignmentWhitespaceSubscriptionId_ThrowsMissingRequiredArgument()
    {
        string whitespaceSubscriptionId = " ";
        using PowerShell shell = CreateShell<RemoveRoleAssignmentCmdlet>();
        shell.AddParameter("Name", AssignmentName)
            .AddParameter("SubscriptionId", whitespaceSubscriptionId)
            .AddParameter("Confirm", false);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            NoScopeMessage,
            Assert.IsType<MissingRequiredArgumentException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // [ValidateRoleAssignmentName] rejects the name while binding, before the extension's own GUID check. The binder
    // throws the internal ParameterBindingValidationException, so the type is matched by its public base.
    [Fact]
    public void Invoke_RemoveRoleAssignmentNameNotGuid_ThrowsParameterBindingException()
    {
        string nameNotGuid = "not-a-guid";
        using PowerShell shell = CreateShell<RemoveRoleAssignmentCmdlet>();
        shell.AddParameter("SubscriptionId", SubscriptionId)
            .AddParameter("Name", nameNotGuid)
            .AddParameter("Confirm", false);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(NameNotGuidMessage, Assert.IsAssignableFrom<ParameterBindingException>(exception).Message);
    }

    // Unlike New-AzRoleAssignment, the attribute here does not allow a blank name.
    [Fact]
    public void Invoke_RemoveRoleAssignmentWhitespaceName_ThrowsParameterBindingException()
    {
        string whitespaceName = " ";
        using PowerShell shell = CreateShell<RemoveRoleAssignmentCmdlet>();
        shell.AddParameter("SubscriptionId", SubscriptionId)
            .AddParameter("Name", whitespaceName)
            .AddParameter("Confirm", false);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Cannot validate argument on parameter 'Name'. The role assignment name ' ' must be a GUID.",
            Assert.IsAssignableFrom<ParameterBindingException>(exception).Message);
    }

    // Scope and ResourceGroupName are in different parameter sets, so PowerShell rejects both together before the
    // extension's "not both" check.
    [Fact]
    public void Invoke_RemoveRoleAssignmentScopeAndResourceGroupName_ThrowsParameterBindingException()
    {
        string resourceGroupName = "rg";
        using PowerShell shell = CreateShell<RemoveRoleAssignmentCmdlet>();
        shell.AddParameter("Name", AssignmentName)
            .AddParameter("Scope", "/subscriptions/sub/resourceGroups/rg")
            .AddParameter("ResourceGroupName", resourceGroupName)
            .AddParameter("Confirm", false);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(ParameterSetMessage, Assert.IsType<ParameterBindingException>(exception).Message);
    }
}
