// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry;

internal static class DataBoundaryRouting
{
    internal const string Eu = "EU";
    internal const string Global = "Global";

    internal static string Normalize(string? dataBoundary) =>
        string.Equals(dataBoundary, Eu, StringComparison.OrdinalIgnoreCase) ? Eu : Global;

    internal static bool IsEu(string? dataBoundary) =>
        string.Equals(Normalize(dataBoundary), Eu, StringComparison.OrdinalIgnoreCase);
}