// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Accounts;

/// <summary>
/// Resolves the three mutually-exclusive token-target inputs — a v1.0 resource URL, a well-known
/// resource-type shortcut, or a v2.0 scope list — into the MSAL scope array passed to
/// <see cref="IAuthService.AcquireTokenSilentlyAsync"/>. Shared by both the CLI and PowerShell front
/// ends so identical inputs produce identical scopes across the CLI and PowerShell surfaces.
/// </summary>
internal static class TokenScopeResolver
{
    private const string DefaultScopeSuffix = "/.default";

    /// <summary>
    /// Resolves the supplied target inputs into a scope array.
    /// <list type="bullet">
    /// <item>At most one of <paramref name="resource"/>, <paramref name="resourceTypeName"/>, or
    /// <paramref name="scopes"/> may be supplied; supplying more than one raises a usage error
    /// (<see cref="AzclipsErrorException"/> from <see cref="AzclipsErrorException.UsageError"/>).</item>
    /// <item>A resource URL resolves to the single scope <c>&lt;resource&gt;/.default</c>.</item>
    /// <item>A well-known resource-type shortcut resolves via
    /// <see cref="WellKnownResourceTypes"/> to its resource URL, then to a <c>.default</c> scope.</item>
    /// <item>An explicit scope list is passed through unchanged.</item>
    /// <item>When none is supplied, defaults to Azure Resource Manager (ARM).</item>
    /// </list>
    /// </summary>
    /// <param name="resource">A v1.0 resource URL (e.g. <c>https://vault.azure.net</c>).</param>
    /// <param name="resourceTypeName">A well-known resource-type shortcut (CLI or PS spelling).</param>
    /// <param name="scopes">One or more v2.0 scopes.</param>
    /// <returns>The resolved, non-empty scope array.</returns>
    /// <exception cref="AzclipsErrorException">A usage error (exit code 2): more than one target input was supplied, or a supplied value was invalid.</exception>
    public static string[] Resolve(string? resource, string? resourceTypeName, IReadOnlyList<string>? scopes)
    {
        bool hasResource = resource is not null;
        bool hasResourceType = resourceTypeName is not null;
        bool hasScopes = scopes is not null;

        int provided = (hasResource ? 1 : 0) + (hasResourceType ? 1 : 0) + (hasScopes ? 1 : 0);
        if (provided > 1)
        {
            throw AzclipsErrorException.UsageError(
                "Specify at most one of a resource, a resource type, or a scope list - these inputs are mutually exclusive.");
        }

        if (hasScopes)
        {
            if (scopes!.Count == 0)
            {
                throw AzclipsErrorException.UsageError(
                    "At least one scope must be supplied with --scope/-Scope.");
            }

            var normalized = new List<string>(scopes.Count);
            foreach (var scope in scopes!)
            {
                if (string.IsNullOrWhiteSpace(scope))
                {
                    throw AzclipsErrorException.UsageError("Scope values must not be empty.");
                }
                normalized.Add(scope.Trim());
            }
            return [.. normalized];
        }

        if (hasResourceType)
        {
            if (string.IsNullOrWhiteSpace(resourceTypeName))
            {
                throw AzclipsErrorException.UsageError("Resource type must not be empty.");
            }

            if (!WellKnownResourceTypes.TryResolve(resourceTypeName, out var resolvedResource))
            {
                throw AzclipsErrorException.UsageError(
                    $"Unknown resource type '{resourceTypeName}'. Known resource types: {string.Join(", ", WellKnownResourceTypes.Names)}.");
            }
            return [ToDefaultScope(resolvedResource)];
        }

        if (hasResource)
        {
            if (string.IsNullOrWhiteSpace(resource))
            {
                throw AzclipsErrorException.UsageError("Resource must not be empty.");
            }
            return [ToDefaultScope(resource.Trim())];
        }

        // No target specified: default to ARM (parity with Azure CLI / Az PowerShell).
        WellKnownResourceTypes.TryResolve(WellKnownResourceTypes.Arm, out var armResource);
        return [ToDefaultScope(armResource)];
    }

    /// <summary>
    /// Converts a resource URL to its default scope by appending <c>/.default</c>. The trailing
    /// slash of the resource is preserved (matching Azure CLI's <c>resource_to_scopes</c>), so
    /// <c>https://management.core.windows.net/</c> becomes
    /// <c>https://management.core.windows.net//.default</c> and
    /// <c>https://managedhsm.azure.net</c> becomes <c>https://managedhsm.azure.net/.default</c>. An
    /// existing <c>/.default</c> suffix is left intact.
    /// </summary>
    public static string ToDefaultScope(string resource)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            throw AzclipsErrorException.InvalidInput("Resource must not be empty.");
        }

        var trimmed = resource.Trim();
        return trimmed.EndsWith(DefaultScopeSuffix, StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : trimmed + DefaultScopeSuffix;
    }
}
