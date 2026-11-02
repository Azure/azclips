// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using System.Diagnostics;

namespace Core.Telemetry;

internal static class DataBoundaryRouting
{
    internal const string Eu = "EU";
    internal const string Global = "Global";

    /// <summary>
    /// No confirmed boundary. The resolution outcome determines the fallback destination.
    /// </summary>
    internal const string Unknown = "Unknown";

    /// <summary>
    /// Normalizes known values and preserves unrecognized values in legacy trace records.
    /// </summary>
    internal static string Normalize(string? dataBoundary) =>
        TelemetryInvocationContext.NormalizeDataBoundary(dataBoundary);

    internal static bool IsEu(string? dataBoundary) =>
        string.Equals(Normalize(dataBoundary), Eu, StringComparison.OrdinalIgnoreCase);

    internal static bool IsConfirmedGlobal(string? dataBoundary) =>
        string.Equals(Normalize(dataBoundary), Global, StringComparison.OrdinalIgnoreCase);

    internal static bool IsConfirmed(string? dataBoundary) =>
        IsEu(dataBoundary) || IsConfirmedGlobal(dataBoundary);

    internal static UploadRegion ResolveUploadRegion(Activity activity) =>
        UploadRegions.Resolve(
            ResolveTag(activity, TelemetryTags.UploadRegion),
            ResolveTag(activity, TelemetryTags.DataBoundary),
            ResolveTag(activity, TelemetryTags.DataBoundaryOutcome));

    private static string? ResolveTag(Activity activity, string key)
    {
        for (var current = activity; current is not null; current = current.Parent)
        {
            if (current.GetTagItem(key) is string value && !string.IsNullOrEmpty(value))
                return value;
        }
        return null;
    }

    internal static DataBoundaryOutcome? ParseOutcome(string? outcome) =>
        Enum.TryParse<DataBoundaryOutcome>(outcome, ignoreCase: true, out var parsed) &&
        Enum.IsDefined(parsed) && string.Equals(outcome, parsed.ToString(), StringComparison.OrdinalIgnoreCase)
            ? parsed
            : null;

}