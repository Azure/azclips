// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.MsGraph;

/// <summary>
/// Shared algorithm for composing the OData <c>$select</c> query parameter value from a
/// resource-specific default property list, an optional caller-supplied override, and the
/// <c>-AppendSelected</c> switch. Per-resource callers pass their own defaults
/// (see <see cref="GraphUserDefaults"/> and <see cref="GraphGroupDefaults"/>).
/// </summary>
internal static class GraphSelectClause
{
    /// <summary>
    /// Builds the value for the OData <c>$select</c> query parameter. Returns <c>null</c> when
    /// nothing should be emitted. When <paramref name="appendSelected"/> is true the
    /// <paramref name="defaults"/> are prepended; duplicates are removed case-insensitively;
    /// the first spelling wins.
    /// </summary>
    public static string? Build(
        IReadOnlyList<string> defaults,
        IReadOnlyList<string>? select,
        bool appendSelected)
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
            var trimmed = candidate.Trim();
            if (seen.Add(trimmed))
            {
                ordered.Add(trimmed);
            }
        }

        if (appendSelected)
        {
            foreach (var def in defaults)
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
