// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Resolves the three mutually-exclusive token-target inputs — a v1.0 resource URL, a well-known
/// resource-type shortcut, or a v2.0 scope list — into the MSAL scope array passed to
/// <see cref="IAuthService.AcquireTokenSilentlyAsync"/>. Shared by both the CLI and PowerShell front
/// ends so identical inputs produce identical scopes (supports Gap T-3 / REQ-3 and T-4 / REQ-4).
/// </summary>
public static class TokenScopeResolver
{
    private const string DefaultScopeSuffix = "/.default";

    /// <summary>
    /// Resolves the supplied target inputs into a scope array.
    /// <list type="bullet">
    /// <item>At most one of <paramref name="resource"/>, <paramref name="resourceTypeName"/>, or
    /// <paramref name="scopes"/> may be supplied; supplying more than one throws
    /// <see cref="ArgumentException"/> (fail fast).</item>
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
    /// <exception cref="ArgumentException">More than one target input was supplied, or a supplied value was invalid.</exception>
    public static string[] Resolve(string? resource, string? resourceTypeName, IReadOnlyList<string>? scopes)
    {
        bool hasResource = !string.IsNullOrWhiteSpace(resource);
        bool hasResourceType = !string.IsNullOrWhiteSpace(resourceTypeName);
        bool hasScopes = scopes is { Count: > 0 };

        int provided = (hasResource ? 1 : 0) + (hasResourceType ? 1 : 0) + (hasScopes ? 1 : 0);
        if (provided > 1)
        {
            throw new ArgumentException(
                "Specify at most one of a resource, a resource type, or a scope list - these inputs are mutually exclusive.");
        }

        if (hasScopes)
        {
            var normalized = new List<string>(scopes!.Count);
            foreach (var scope in scopes!)
            {
                if (string.IsNullOrWhiteSpace(scope))
                {
                    throw new ArgumentException("Scope values must not be empty.", nameof(scopes));
                }
                normalized.Add(scope.Trim());
            }
            return [.. normalized];
        }

        if (hasResourceType)
        {
            if (!WellKnownResourceTypes.TryResolve(resourceTypeName, out var resolvedResource))
            {
                throw new ArgumentException(
                    $"Unknown resource type '{resourceTypeName}'. Known resource types: {string.Join(", ", WellKnownResourceTypes.Names)}.",
                    nameof(resourceTypeName));
            }
            return [ToDefaultScope(resolvedResource)];
        }

        if (hasResource)
        {
            return [ToDefaultScope(resource!.Trim())];
        }

        // No target specified: default to ARM (parity with Azure CLI / Az PowerShell).
        WellKnownResourceTypes.TryResolve(WellKnownResourceTypes.Arm, out var armResource);
        return [ToDefaultScope(armResource)];
    }

    /// <summary>
    /// Converts a resource URL to its default scope by appending <c>/.default</c>, avoiding a double
    /// slash if the resource already ends in <c>/</c> and leaving an existing <c>/.default</c> intact.
    /// </summary>
    public static string ToDefaultScope(string resource)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            throw new ArgumentException("Resource must not be empty.", nameof(resource));
        }

        var trimmed = resource.Trim();
        if (trimmed.EndsWith(DefaultScopeSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return trimmed.EndsWith('/')
            ? trimmed + ".default"
            : trimmed + DefaultScopeSuffix;
    }
}
