// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Shared scope resolution for role assignment commands. Both the list and create orchestrators accept
/// an explicit scope, a subscription + resource group, or a bare subscription, so the precedence and
/// normalization live here to avoid duplication (mirrors the repo's other resolver helpers such as
/// <see cref="ResourceGroupIdResolver"/>).
/// </summary>
internal static class RoleAssignmentScope
{
    /// <summary>
    /// Resolves the effective scope with precedence: explicit <paramref name="scope"/> first, then
    /// subscription + <paramref name="resourceGroupName"/>, then the subscription alone.
    /// <paramref name="scope"/> and <paramref name="resourceGroupName"/> are mutually exclusive.
    /// </summary>
    internal static string Resolve(string? subscriptionId, string? scope, string? resourceGroupName)
    {
        bool hasScope = !string.IsNullOrWhiteSpace(scope);
        bool hasResourceGroup = !string.IsNullOrWhiteSpace(resourceGroupName);

        if (hasScope && hasResourceGroup)
        {
            throw AzclipsErrorException.UsageError("Specify either a scope or a resource group, not both.");
        }

        if (hasScope)
        {
            return Normalize(scope!);
        }

        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            throw AzclipsErrorException.MissingRequiredParameters(
                hasResourceGroup
                    ? "A subscription is required to resolve a resource group scope. Specify a subscription, or set an active subscription."
                    : "A scope is required. Specify a scope or a resource group, or set an active subscription.");
        }

        string subscriptionScope = $"/subscriptions/{subscriptionId!.Trim()}";
        return hasResourceGroup
            ? $"{subscriptionScope}/resourceGroups/{resourceGroupName!.Trim()}"
            : subscriptionScope;
    }

    /// <summary>Ensures a leading slash and trims any trailing slash from a user-supplied scope.</summary>
    internal static string Normalize(string scope)
    {
        string effectiveScope = scope.Trim();
        if (!effectiveScope.StartsWith('/'))
        {
            effectiveScope = "/" + effectiveScope;
        }

        return effectiveScope.TrimEnd('/');
    }
}
