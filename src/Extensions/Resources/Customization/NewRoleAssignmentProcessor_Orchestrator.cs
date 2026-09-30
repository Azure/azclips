// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// CLI customization over the spec-shaped <see cref="NewRoleAssignmentProcessor"/>. Resolves the
/// az-style conveniences that are not part of the ARM create body into the atomic's
/// <see cref="NewRoleAssignmentProcessor.Scope"/>, <see cref="NewRoleAssignmentProcessor.RoleAssignmentName"/>,
/// and <see cref="NewRoleAssignmentProcessor.Body"/>:
/// <list type="bullet">
/// <item>Scope is the grant target. It defaults to the active subscription and can be narrowed with
/// <see cref="ResourceGroupName"/> or overridden with an explicit scope (mutually exclusive).</item>
/// <item><see cref="Assignee"/> / <see cref="AssigneeObjectId"/> supply the principal ID. Both accept an
/// object (principal) ID (GUID) only; name/UPN/service-principal resolution needs a Microsoft Graph
/// lookup this ARM-only path does not perform.</item>
/// <item><see cref="RoleDefinitionId"/> (a role definition GUID) is expanded to the fully-qualified
/// role definition ID rooted at the scope's subscription. Role name resolution is not yet supported.</item>
/// <item>A missing <see cref="Name"/> is generated (a fresh GUID), matching the classic tools.</item>
/// </list>
/// </summary>
public class NewRoleAssignmentProcessor_Orchestrator : NewRoleAssignmentProcessor
{
    public string SubscriptionId { get; set; } = string.Empty;

    public string? ResourceGroupName { get; set; }

    public string? Assignee { get; set; }

    public string? AssigneeObjectId { get; set; }

    public string? RoleDefinitionId { get; set; }

    public string? PrincipalType { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Condition { get; set; }

    public string? ConditionVersion { get; set; }

    protected override async Task BeforeProcessAsync()
    {
        string scope = RoleAssignmentScope.Resolve(SubscriptionId, Scope, ResourceGroupName);
        Scope = scope;
        RoleAssignmentName = ResolveName();

        Body = new RoleAssignmentCreateParameters
        {
            Properties = new RoleAssignmentCreateProperties
            {
                PrincipalId = ResolvePrincipalId(),
                RoleDefinitionId = ResolveRoleDefinitionId(scope),
                PrincipalType = PrincipalType,
                Description = Description,
                Condition = Condition,
                ConditionVersion = ConditionVersion,
            },
        };

        await base.BeforeProcessAsync();
    }

    private string ResolveName()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return Guid.NewGuid().ToString();
        }

        string value = Name.Trim();
        if (!Guid.TryParse(value, out _))
        {
            throw AzclipsErrorException.UsageError($"The role assignment name '{value}' must be a GUID.");
        }

        return value;
    }

    /// <summary>
    /// Resolves the principal object ID. <see cref="AssigneeObjectId"/> is trusted; <see cref="Assignee"/>
    /// is accepted only when it is already an object ID (GUID). Exactly one must be supplied.
    /// </summary>
    private string ResolvePrincipalId()
    {
        bool hasAssignee = !string.IsNullOrWhiteSpace(Assignee);
        bool hasObjectId = !string.IsNullOrWhiteSpace(AssigneeObjectId);

        if (hasAssignee && hasObjectId)
        {
            throw AzclipsErrorException.UsageError("Specify only one assignee.");
        }

        if (!hasAssignee && !hasObjectId)
        {
            throw AzclipsErrorException.MissingRequiredParameters(
                "A principal is required. Specify the principal's object ID (GUID) to assign the role to.");
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

        string assignee = Assignee!.Trim();
        if (!Guid.TryParse(assignee, out _))
        {
            throw AzclipsErrorException.UsageError(
                $"The assignee '{assignee}' is not an object ID (GUID). Name, UPN, and service-principal resolution is not yet supported; " +
                "specify the principal's object ID (GUID).");
        }

        return assignee;
    }

    /// <summary>
    /// Expands the required <see cref="RoleDefinitionId"/> into a fully-qualified role definition ID.
    /// A value that is already a path is used as-is; a GUID is rooted at the grant scope's subscription
    /// (or the scope itself for management-group / tenant scopes). Role name resolution is not yet supported.
    /// </summary>
    private string ResolveRoleDefinitionId(string scope)
    {
        if (string.IsNullOrWhiteSpace(RoleDefinitionId))
        {
            throw AzclipsErrorException.MissingRequiredParameters("A role is required. Specify the role definition ID (GUID).");
        }

        string value = RoleDefinitionId.Trim();
        if (value.StartsWith('/'))
        {
            return value;
        }

        if (!Guid.TryParse(value, out _))
        {
            throw AzclipsErrorException.UsageError(
                $"The role '{value}' is not a role definition ID (GUID). Role name resolution is not yet supported; " +
                "specify the role definition ID (GUID).");
        }

        return $"{RoleDefinitionRoot(scope)}/providers/Microsoft.Authorization/roleDefinitions/{value}";
    }

    // Roots the role definition at the scope's subscription when present, else at the scope itself.
    private static string RoleDefinitionRoot(string scope)
    {
        const string subscriptions = "/subscriptions/";
        if (scope.StartsWith(subscriptions, StringComparison.OrdinalIgnoreCase))
        {
            int next = scope.IndexOf('/', subscriptions.Length);
            return next < 0 ? scope : scope[..next];
        }

        return scope;
    }
}
