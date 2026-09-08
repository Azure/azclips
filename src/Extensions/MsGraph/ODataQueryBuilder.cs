// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.MsGraph;

/// <summary>
/// Shared helper for composing the OData query string portion of a Microsoft Graph URL.
/// Emits <c>?$filter=...&amp;$select=...&amp;$expand=...&amp;$orderby=...&amp;$search=...</c>
/// with a consistent parameter order across every atomic processor. Handles URL-encoding
/// and comma-joining of array-valued parameters. Returns an empty string when no arguments
/// are set, so callers can safely append the return value to a base URL unconditionally.
/// </summary>
internal static class ODataQueryBuilder
{
    /// <summary>
    /// Builds the query-string portion of a Microsoft Graph request URL. Any argument that is
    /// null or empty is omitted. Parameter order in the emitted string is fixed:
    /// filter, select, expand, orderby, search — matching the order every atomic processor used
    /// prior to consolidation, so recorded HTTP fixtures remain byte-identical.
    /// </summary>
    /// <returns>
    /// A leading-'?' query string when at least one argument is present (for example
    /// <c>?$filter=accountEnabled%20eq%20true&amp;$select=id%2CdisplayName</c>), or an empty
    /// string when every argument is empty.
    /// </returns>
    public static string Build(
        string? filter = null,
        string[]? select = null,
        string[]? expand = null,
        string[]? orderby = null,
        string? search = null)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            parts.Add($"$filter={Uri.EscapeDataString(filter)}");
        }

        if (select is { Length: > 0 })
        {
            parts.Add($"$select={Uri.EscapeDataString(string.Join(',', select))}");
        }

        if (expand is { Length: > 0 })
        {
            parts.Add($"$expand={Uri.EscapeDataString(string.Join(',', expand))}");
        }

        if (orderby is { Length: > 0 })
        {
            parts.Add($"$orderby={Uri.EscapeDataString(string.Join(',', orderby))}");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            parts.Add($"$search={Uri.EscapeDataString(search)}");
        }

        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
    }
}
