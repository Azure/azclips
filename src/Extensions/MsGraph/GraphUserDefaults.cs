// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.MsGraph;

/// <summary>
/// Shared helpers for Microsoft Graph <c>/users</c> cmdlets.
/// </summary>
public static class GraphUserDefaults
{
    /// <summary>
    /// Default Microsoft Graph user property set injected by <c>-AppendSelected</c>, in the
    /// camelCase form Microsoft Graph accepts in <c>$select</c>.
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
    /// property list and the <c>-AppendSelected</c> switch.
    /// </summary>
    public static string? BuildSelectClause(IReadOnlyList<string>? select, bool appendSelected) =>
        GraphSelectClause.Build(DefaultSelectProperties, select, appendSelected);
}
