// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Telemetry;

internal static class DataBoundaryRouting
{
    internal const string Eu = "EU";
    internal const string Row = "ROW";

    /// <summary>
    /// Normalizes an already-resolved destination for export. The resolver explicitly supplies
    /// EU for authenticated lookup failures; absent or legacy non-EU values default to ROW.
    /// </summary>
    internal static string Normalize(string? dataBoundary) =>
        TelemetryInvocationContext.NormalizeDataBoundary(dataBoundary);

    /// <summary>True when the resolved telemetry destination is EU.</summary>
    internal static bool IsEu(string? dataBoundary) =>
        string.Equals(Normalize(dataBoundary), Eu, StringComparison.OrdinalIgnoreCase);
}
