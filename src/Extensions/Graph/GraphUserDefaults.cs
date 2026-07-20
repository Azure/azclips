// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Graph;

/// <summary>
/// Shared helpers for Microsoft Graph <c>/users</c> cmdlets. Mirrors Az.Resources
/// <c>Get-AzADUser</c> so callers get identical output.
/// </summary>
public static class GraphUserDefaults
{
    /// <summary>
    /// Default Microsoft Graph user property set injected by <c>-AppendSelected</c>. Copied
    /// verbatim from Az.Resources <c>Get-AzADUser -AppendSelected</c>, in the camelCase form
    /// Microsoft Graph accepts in <c>$select</c>.
    /// </summary>
    public static readonly string[] DefaultSelectProperties =
    [
        "displayName",
        "id",
        "deletedDateTime",
        "userPrincipalName",
        "usageLocation",
        "givenName",
        "surname",
        "accountEnabled",
        "mailNickname",
        "mail",
    ];

    /// <summary>
    /// Builds the value for the OData <c>$select</c> query parameter from a caller-supplied
    /// property list and the <c>-AppendSelected</c> switch. Returns <c>null</c> when nothing
    /// should be emitted. When <paramref name="appendSelected"/> is true the defaults are
    /// prepended; duplicates are removed case-insensitively; the first spelling wins.
    /// </summary>
    public static string? BuildSelectClause(IReadOnlyList<string>? select, bool appendSelected)
    {
        var hasSelect = select is { Count: > 0 };
        if (!hasSelect && !appendSelected)
        {
            return null;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();

        void Add(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return;
            if (seen.Add(candidate))
            {
                ordered.Add(candidate);
            }
        }

        if (appendSelected)
        {
            foreach (var def in DefaultSelectProperties)
            {
                Add(def);
            }
        }

        if (hasSelect)
        {
            foreach (var extra in select!)
            {
                Add(extra);
            }
        }

        return ordered.Count == 0 ? null : string.Join(',', ordered);
    }
}
