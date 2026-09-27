// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// CLI customization over the spec-shaped <see cref="ListRoleAssignmentProcessor"/>. Adds the
/// az-style conveniences that are not part of the ARM list operation and resolves them into the
/// atomic's <see cref="ListRoleAssignmentProcessor.Scope"/> and
/// <see cref="ListRoleAssignmentProcessor.Filter"/>:
/// <list type="bullet">
/// <item>Scope defaults to the active subscription and can be narrowed with
/// <see cref="ResourceGroupName"/> or overridden with an explicit scope (mutually exclusive).</item>
/// <item><see cref="Assignee"/> / <see cref="AssigneeObjectId"/> filter server-side by principal.
/// <see cref="Assignee"/> currently accepts only an object (principal) ID; name/UPN/service-principal
/// resolution needs a directory (Microsoft Graph) lookup this ARM-only path does not perform.</item>
/// </list>
/// </summary>
public class ListRoleAssignmentProcessor_Orchestrator : ListRoleAssignmentProcessor
{
    public string SubscriptionId { get; set; } = string.Empty;

    public string? ResourceGroupName { get; set; }

    public string? Assignee { get; set; }

    public string? AssigneeObjectId { get; set; }

    protected override async Task BeforeProcessAsync()
    {
        Scope = ResolveScope();

        if (ResolveAssigneeObjectId() is { } objectId)
        {
            Filter = $"principalId eq '{objectId}'";
        }

        await base.BeforeProcessAsync();
    }

    /// <summary>
    /// Resolves the effective scope with precedence: explicit <see cref="ListRoleAssignmentProcessor.Scope"/>
    /// first, then subscription + <see cref="ResourceGroupName"/>, then the subscription alone.
    /// <see cref="ListRoleAssignmentProcessor.Scope"/> and <see cref="ResourceGroupName"/> are mutually
    /// exclusive.
    /// </summary>
    private string ResolveScope()
    {
        bool hasScope = !string.IsNullOrWhiteSpace(Scope);
        bool hasResourceGroup = !string.IsNullOrWhiteSpace(ResourceGroupName);

        if (hasScope && hasResourceGroup)
        {
            throw AzclipsErrorException.UsageError("Specify either --scope or --resource-group, not both.");
        }

        if (hasScope)
        {
            return NormalizeScope(Scope);
        }

        if (string.IsNullOrWhiteSpace(SubscriptionId))
        {
            throw AzclipsErrorException.MissingRequiredParameters(
                "A subscription is required to list role assignments. Provide --scope, or set an active subscription.");
        }

        string subscriptionScope = $"/subscriptions/{SubscriptionId.Trim()}";
        return hasResourceGroup
            ? $"{subscriptionScope}/resourceGroups/{ResourceGroupName!.Trim()}"
            : subscriptionScope;
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
            throw AzclipsErrorException.UsageError("Specify only one of --assignee or --assignee-object-id.");
        }

        if (hasObjectId)
        {
            string objectId = AssigneeObjectId!.Trim();
            if (!Guid.TryParse(objectId, out _))
            {
                throw AzclipsErrorException.UsageError(
                    $"--assignee-object-id '{objectId}' is not a valid object ID (GUID).");
            }

            return objectId;
        }

        if (hasAssignee)
        {
            string value = Assignee!.Trim();
            if (!Guid.TryParse(value, out _))
            {
                throw AzclipsErrorException.UsageError(
                    $"--assignee '{value}' is not an object ID. Name, UPN, and service-principal resolution is not yet supported; " +
                    "pass the principal's object ID, or use --assignee-object-id.");
            }

            return value;
        }

        return null;
    }

    private static string NormalizeScope(string scope)
    {
        string effectiveScope = scope.Trim();
        if (!effectiveScope.StartsWith('/'))
        {
            effectiveScope = "/" + effectiveScope;
        }

        return effectiveScope.TrimEnd('/');
    }
}
