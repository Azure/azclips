// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.MsGraph;

/// <summary>
/// Shared helpers for Microsoft Graph <c>/groups</c> cmdlets.
/// </summary>
public static class GraphGroupDefaults
{
    /// <summary>
    /// Default Microsoft Graph group property set injected by <c>-AppendSelected</c>, in the
    /// camelCase form Microsoft Graph accepts in <c>$select</c>.
    /// </summary>
    public static readonly string[] DefaultSelectProperties =
    [
        "displayName",
        "id",
        "deletedDateTime",
        "securityEnabled",
        "mailEnabled",
        "mailNickname",
        "description",
    ];

    /// <summary>
    /// Builds the value for the OData <c>$select</c> query parameter from a caller-supplied
    /// property list and the <c>-AppendSelected</c> switch.
    /// </summary>
    public static string? BuildSelectClause(IReadOnlyList<string>? select, bool appendSelected) =>
        GraphSelectClause.Build(DefaultSelectProperties, select, appendSelected);
}
