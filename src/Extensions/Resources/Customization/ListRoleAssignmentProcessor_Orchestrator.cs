// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// CLI customization over the spec-shaped <see cref="ListRoleAssignmentProcessor"/>. Adds the
/// az-style conveniences that are not part of the ARM list operation and resolves them into the
/// atomic's <see cref="ListRoleAssignmentProcessor.Scope"/> and
/// <see cref="ListRoleAssignmentProcessor.Filter"/>, plus the client-side trims ARM cannot express
/// server-side.
/// <para>
/// Scope defaults to the active subscription and can be narrowed with <see cref="ResourceGroupName"/>
/// or overridden with an explicit scope (mutually exclusive). Scope breadth mirrors classic
/// (<c>az role assignment list</c> / <c>Get-AzRoleAssignment</c>): ARM has no "exactly this scope"
/// filter, so the model is <c>atScope()</c> (which returns the scope and its ancestors, never
/// subscopes) plus a client-side trim:
/// <list type="bullet">
/// <item><b>Default</b>: <c>atScope()</c> + trim to the exact scope — only assignments made at this
/// scope.</item>
/// <item><see cref="IncludeInherited"/> (<c>--include-inherited</c> / <c>-AtScope</c>): <c>atScope()</c>
/// with no trim — this scope plus inherited (ancestor) assignments.</item>
/// <item><see cref="All"/> (<c>--all</c>, CLI only): no <c>atScope()</c> filter at the subscription —
/// the subscription and every nested scope beneath it. Mutually exclusive with an explicit scope or
/// resource group.</item>
/// <item><see cref="IncludeGroups"/> (<c>--include-groups</c> / <c>-ExpandPrincipalGroups</c>): when an
/// assignee is supplied, filters with <c>assignedTo('{id}')</c>, which ARM expands transitively through
/// group membership (no directory/Graph lookup).</item>
/// </list>
/// </para>
/// <para>
/// <see cref="Assignee"/> / <see cref="AssigneeObjectId"/> currently accept only an object (principal)
/// ID; name/UPN/service-principal resolution needs a Microsoft Graph lookup this ARM-only path does not
/// perform. ARM rejects <c>atScope()</c> combined with <c>principalId eq</c>, so a scoped principal
/// filter is applied client-side instead.
/// </para>
/// </summary>
public class ListRoleAssignmentProcessor_Orchestrator : ListRoleAssignmentProcessor
{
    public string SubscriptionId { get; set; } = string.Empty;

    public string? ResourceGroupName { get; set; }

    public string? Assignee { get; set; }

    public string? AssigneeObjectId { get; set; }

    public string? RoleDefinitionId { get; set; }

    /// <summary>Include assignments inherited from ancestor scopes (CLI <c>--include-inherited</c> / PS <c>-AtScope</c>).</summary>
    public bool IncludeInherited { get; set; }

    /// <summary>List across the whole subscription including every nested scope (CLI <c>--all</c>).</summary>
    public bool All { get; set; }

    /// <summary>Expand the assignee filter through group membership (CLI <c>--include-groups</c> / PS <c>-ExpandPrincipalGroups</c>).</summary>
    public bool IncludeGroups { get; set; }

    // Client-side trims resolved in BeforeProcessAsync. ARM cannot express either alongside atScope(),
    // so they are applied per page in FilterPageItems.
    private string? exactScope;      // keep only assignments whose scope equals this (case-insensitive)
    private string? principalIdTrim; // keep only assignments for this principal object ID

    protected override async Task BeforeProcessAsync()
    {
        string? objectId = ResolveAssigneeObjectId();
        ValidateRoleDefinitionId();

        if (All)
        {
            // --all lists the whole subscription (every nested scope), so an explicit scope or
            // resource group is contradictory, matching classic's "group or scope are not required
            // when --all is used".
            if (Scope is not null || ResourceGroupName is not null)
            {
                throw AzclipsErrorException.UsageError(
                    "--all lists role assignments across the whole subscription and cannot be combined with a scope or resource group.");
            }

            if (string.IsNullOrWhiteSpace(SubscriptionId))
            {
                throw AzclipsErrorException.MissingRequiredParameters(
                    "A subscription is required to list all role assignments. Specify a subscription, or set an active subscription.");
            }

            Scope = $"/subscriptions/{SubscriptionId.Trim()}";

            // No atScope() filter: a plain List For Scope at the subscription returns assignments at
            // the subscription and every nested scope beneath it. A principal filter can be applied
            // server-side here because it is not combined with atScope().
            Filter = objectId is null
                ? null
                : IncludeGroups
                    ? $"assignedTo('{objectId}')"
                    : $"principalId eq '{objectId}'";
        }
        else
        {
            Scope = RoleAssignmentScope.Resolve(SubscriptionId, Scope, ResourceGroupName);

            if (objectId is not null && IncludeGroups)
            {
                // atScope() + assignedTo() is the one server-side combination ARM allows; assignedTo()
                // expands group membership transitively (ARM-side, no directory lookup).
                Filter = $"atScope() and assignedTo('{objectId}')";
            }
            else
            {
                // atScope() returns assignments effective at this scope (this scope and its ancestors),
                // never subscopes.
                Filter = "atScope()";

                // ARM rejects atScope() combined with "principalId eq", so filter the principal
                // client-side instead.
                principalIdTrim = objectId;
            }

            // By default show only assignments made at exactly this scope; --include-inherited keeps
            // the ancestor (inherited) assignments atScope() also returns.
            if (!IncludeInherited)
            {
                exactScope = Scope;
            }
        }

        await base.BeforeProcessAsync();
    }

    /// <summary>
    /// Applies the client-side filters ARM cannot evaluate alongside <c>atScope()</c>: the <c>--role</c>
    /// role-definition match, the exact-scope trim (default, non-inherited listing), and the scoped
    /// principal trim. Applied per page so pagination still follows <c>nextLink</c> over the unfiltered
    /// result set.
    /// </summary>
    protected override IEnumerable<RoleAssignment> FilterPageItems(IEnumerable<RoleAssignment> items)
    {
        IEnumerable<RoleAssignment> result = items;

        if (!string.IsNullOrWhiteSpace(RoleDefinitionId))
        {
            string role = RoleDefinitionId.Trim();
            result = result.Where(assignment => MatchesRoleDefinition(assignment, role));
        }

        if (exactScope is not null)
        {
            result = result.Where(assignment =>
                string.Equals(assignment.Properties?.Scope, exactScope, StringComparison.OrdinalIgnoreCase));
        }

        if (principalIdTrim is not null)
        {
            result = result.Where(assignment =>
                string.Equals(assignment.Properties?.PrincipalId, principalIdTrim, StringComparison.OrdinalIgnoreCase));
        }

        return result;
    }

    private static bool MatchesRoleDefinition(RoleAssignment assignment, string roleDefinitionId)
    {
        string? id = assignment.Properties?.RoleDefinitionId;
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        string tail = id[(id.LastIndexOf('/') + 1)..];
        return string.Equals(tail, roleDefinitionId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates the role filter. Only a role definition ID (GUID) is supported; resolving a role name to
    /// its definition ID needs an extra ARM roleDefinitions lookup that is not yet performed.
    /// </summary>
    private void ValidateRoleDefinitionId()
    {
        if (string.IsNullOrWhiteSpace(RoleDefinitionId))
        {
            return;
        }

        RoleDefinitionId = RoleDefinitionId.Trim();
        if (!Guid.TryParse(RoleDefinitionId, out _))
        {
            throw AzclipsErrorException.UsageError(
                $"The role '{RoleDefinitionId}' is not a role definition ID (GUID). Role name resolution is not yet supported; " +
                "specify the role definition ID (GUID).");
        }
    }

    /// <summary>
    /// Resolves the principal object ID to filter by, or null when no assignee was supplied.
    /// Both <see cref="Assignee"/> and <see cref="AssigneeObjectId"/> must be an object ID (GUID) — a
    /// name, UPN, or service-principal name requires a Microsoft Graph lookup that is not yet supported.
    /// </summary>
    private string? ResolveAssigneeObjectId()
    {
        bool hasAssignee = !string.IsNullOrWhiteSpace(Assignee);
        bool hasObjectId = !string.IsNullOrWhiteSpace(AssigneeObjectId);

        if (hasAssignee && hasObjectId)
        {
            throw AzclipsErrorException.UsageError("Specify only one assignee.");
        }

        if (hasObjectId)
        {
            string objectId = AssigneeObjectId!.Trim();
            if (!Guid.TryParse(objectId, out _))
            {
                throw AzclipsErrorException.UsageError(
                    $"The assignee object ID '{objectId}' is not a valid object ID (GUID).");
            }

            return objectId;
        }

        if (hasAssignee)
        {
            string value = Assignee!.Trim();
            if (!Guid.TryParse(value, out _))
            {
                throw AzclipsErrorException.UsageError(
                    $"The assignee '{value}' is not an object ID (GUID). Name, UPN, and service-principal resolution is not yet supported; " +
                    "specify the principal's object ID (GUID).");
            }

            return value;
        }

        return null;
    }
}
