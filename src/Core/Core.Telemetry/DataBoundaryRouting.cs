// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

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

    internal static DataBoundaryOutcome? ParseOutcome(string? outcome) =>
        Enum.TryParse<DataBoundaryOutcome>(outcome, ignoreCase: true, out var parsed) &&
        Enum.IsDefined(parsed) && string.Equals(outcome, parsed.ToString(), StringComparison.OrdinalIgnoreCase)
            ? parsed
            : null;

    internal static RegionalDestination ResolveDestination(string? dataBoundary, string? outcome = null) =>
        DataBoundaryRoutingPolicy.Default.Resolve(dataBoundary, ParseOutcome(outcome));
}