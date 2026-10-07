// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Cli.Tests;

/// <summary>
/// PowerShell metadata tests for <c>Get-AzRoleAssignment</c>. The cmdlet reuses the same
/// <see cref="ListRoleAssignmentProcessor_Orchestrator"/> as the CLI <c>az role assignment list</c>;
/// only the parameter names differ (PascalCase, and <c>-ObjectId</c> maps onto the processor's
/// <c>AssigneeObjectId</c> property). The metadata <c>Name</c> is what the argument binder uses to set
/// each processor property, so asserting the mapping here guards the PS surface without a separate
/// binding harness (consistent with how the sibling PowerShell commands are tested in this project).
/// </summary>
public class RoleAssignmentPsTests
{
    [Fact]
    public void PowerShellMetadata_ExposesGetAzRoleAssignment()
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("Get-AzRoleAssignment", ClientType.PowerShell);

        Assert.Equal(typeof(ListRoleAssignmentProcessor_Orchestrator), metadata.ProcessorType);

        // PS parameter name -> processor property name mapping (what the binder uses).
        Assert.Equal("SubscriptionId", metadata.Parameters["SubscriptionId"].Name);
        Assert.Equal("Scope", metadata.Parameters["Scope"].Name);
        Assert.Equal("ResourceGroupName", metadata.Parameters["ResourceGroupName"].Name);
        Assert.Equal("AssigneeObjectId", metadata.Parameters["ObjectId"].Name);
        Assert.Equal("RoleDefinitionId", metadata.Parameters["RoleDefinitionId"].Name);
        Assert.Equal("IncludeInherited", metadata.Parameters["AtScope"].Name);
        Assert.Equal(typeof(bool), metadata.Parameters["AtScope"].ParameterType);
        Assert.Equal("IncludeGroups", metadata.Parameters["ExpandPrincipalGroups"].Name);
        Assert.Equal(typeof(bool), metadata.Parameters["ExpandPrincipalGroups"].ParameterType);
        // The CLI's --all nested-scope flag is exposed on PowerShell as -IncludeNestedScopes (the CLI
        // spelling --all would be unidiomatic for a PowerShell switch); both bind the same All property.
        Assert.Equal("All", metadata.Parameters["IncludeNestedScopes"].Name);
        Assert.Equal(typeof(bool), metadata.Parameters["IncludeNestedScopes"].ParameterType);

        Assert.False(metadata.Parameters["SubscriptionId"].IsRequired);
        Assert.False(metadata.Parameters["Scope"].IsRequired);
        Assert.False(metadata.Parameters["ResourceGroupName"].IsRequired);
        Assert.False(metadata.Parameters["ObjectId"].IsRequired);
        Assert.False(metadata.Parameters["RoleDefinitionId"].IsRequired);

        // CLI-only spellings are not part of the PowerShell surface.
        Assert.DoesNotContain("assignee", metadata.Parameters.Keys);
        Assert.DoesNotContain("assignee-object-id", metadata.Parameters.Keys);
        Assert.DoesNotContain("resource-group", metadata.Parameters.Keys);
        Assert.DoesNotContain("all", metadata.Parameters.Keys);
    }

    [Fact]
    public void PowerShellMetadata_ExposesNewAzRoleAssignment()
    {
        CommandMetadata metadata = new ExtensionIndex().GetCommandMetadata("New-AzRoleAssignment", ClientType.PowerShell);

        Assert.Equal(typeof(NewRoleAssignmentProcessor_Orchestrator), metadata.ProcessorType);

        // PS parameter name -> processor property mapping.
        Assert.Equal("AssigneeObjectId", metadata.Parameters["ObjectId"].Name);
        Assert.Equal("RoleDefinitionId", metadata.Parameters["RoleDefinitionId"].Name);
        Assert.Equal("PrincipalType", metadata.Parameters["ObjectType"].Name);

        Assert.True(metadata.Parameters["ObjectId"].IsRequired);
        Assert.True(metadata.Parameters["RoleDefinitionId"].IsRequired);
        Assert.False(metadata.Parameters["Scope"].IsRequired);
    }
}
