// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// CLI customization over the spec-shaped <see cref="DeleteRoleAssignmentProcessor"/>. Resolves the
/// az-style conveniences that are not part of the ARM delete call into the atomic's
/// <see cref="DeleteRoleAssignmentProcessor.Scope"/> and
/// <see cref="DeleteRoleAssignmentProcessor.RoleAssignmentName"/>:
/// <list type="bullet">
/// <item>Scope is the delete target. An explicit scope is used as-is; otherwise it is built from the
/// subscription, optionally narrowed by <see cref="ResourceGroupName"/>. An explicit scope and a
/// resource group are mutually exclusive.</item>
/// <item><see cref="Name"/> (the role assignment's GUID) is required. Unlike create, there is no
/// "generate a fresh one" fallback: a delete must target one already-known, specific assignment. Use
/// <c>role assignment list</c> / <c>Get-AzRoleAssignment</c> first to find it.</item>
/// </list>
/// Deliberately scoped to this single-resource delete (by scope + name) only. Classic
/// <c>az role assignment delete</c> / <c>Remove-AzRoleAssignment</c> support deleting by a bare
/// principal/role/scope filter, which can match and delete more than one assignment — a
/// well-documented footgun (github.com/Azure/azure-cli/issues/13577). That filter-based delete
/// mode is intentionally not supported here.
/// </summary>
public class DeleteRoleAssignmentProcessor_Orchestrator : DeleteRoleAssignmentProcessor
{
    public string SubscriptionId { get; set; } = string.Empty;

    public string? ResourceGroupName { get; set; }

    public string? Name { get; set; }

    protected override async Task BeforeProcessAsync()
    {
        Scope = RoleAssignmentScope.Resolve(SubscriptionId, Scope, ResourceGroupName);
        RoleAssignmentName = ResolveName();

        await base.BeforeProcessAsync();
    }

    private string ResolveName()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw AzclipsErrorException.MissingRequiredParameters(
                "A role assignment name is required. Specify the role assignment name (GUID) to delete.");
        }

        string value = Name.Trim();
        if (!Guid.TryParse(value, out _))
        {
            throw AzclipsErrorException.UsageError($"The role assignment name '{value}' must be a GUID.");
        }

        return value;
    }
}
