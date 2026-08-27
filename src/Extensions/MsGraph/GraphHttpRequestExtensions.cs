// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.MsGraph;

/// <summary>
/// Extension methods that centralise Microsoft Graph-specific HTTP header handling used by
/// atomic processors. The methods are internal because atomics are the only callers today; if
/// customizations grow their own header needs later they can move to a broader visibility.
/// </summary>
internal static class GraphHttpRequestExtensions
{
    private const string ConsistencyLevelHeaderName = "ConsistencyLevel";

    /// <summary>
    /// Adds the Microsoft Graph <c>ConsistencyLevel</c> header when the caller supplied a
    /// non-empty value. Uses <see cref="System.Net.Http.Headers.HttpHeaders.TryAddWithoutValidation(string, string?)"/>
    /// to bypass .NET's built-in header allow-list, matching what every atomic did inline
    /// prior to consolidation. No-op when <paramref name="consistencyLevel"/> is null or
    /// whitespace, so callers can invoke unconditionally.
    /// </summary>
    /// <param name="request">The outgoing Microsoft Graph request.</param>
    /// <param name="consistencyLevel">
    /// Value to send in the header, typically <c>"eventual"</c> when using advanced queries
    /// such as <c>$search</c>, <c>$count</c>, or <c>$orderby</c> on non-indexed properties.
    /// </param>
    public static void SetConsistencyLevel(this HttpRequestMessage request, string? consistencyLevel)
    {
        if (!string.IsNullOrWhiteSpace(consistencyLevel))
        {
            request.Headers.TryAddWithoutValidation(ConsistencyLevelHeaderName, consistencyLevel);
        }
    }
}
