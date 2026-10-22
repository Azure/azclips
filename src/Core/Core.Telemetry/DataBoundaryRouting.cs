// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Telemetry;

internal static class DataBoundaryRouting
{
    internal const string Eu = "EU";
    internal const string Global = "Global";

    /// <summary>
    /// Sentinel for "we have no boundary value at all" (a lookup that failed, timed out, or
    /// never ran). Deliberately not <see cref="Global"/>: ARM reports <see cref="Global"/> for
    /// a tenant with no boundary resource, which is a real, confirmed answer, while
    /// <see cref="Unknown"/> means we never got one. Collapsing the two would let a failed
    /// lookup be read as permission to route an EU tenant's data outside the EU.
    /// </summary>
    internal const string Unknown = "Unknown";

    /// <summary>
    /// Canonicalizes known literals (case-insensitively) to <see cref="Eu"/>/<see cref="Global"/>.
    /// A missing value becomes <see cref="Unknown"/>; an unrecognized or future raw value from
    /// ARM is returned UNCHANGED rather than rounded to <see cref="Global"/>, so it stays
    /// distinguishable (and visible in telemetry/grouping) from a genuine confirmed answer.
    /// </summary>
    internal static string Normalize(string? dataBoundary) =>
        TelemetryInvocationContext.NormalizeDataBoundary(dataBoundary);

    /// <summary>True only for a confirmed EU boundary.</summary>
    internal static bool IsEu(string? dataBoundary) =>
        string.Equals(Normalize(dataBoundary), Eu, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True only for ARM's confirmed <see cref="Global"/> answer -- false for EU AND for
    /// <see cref="Unknown"/>/unrecognized values. Routing that decides whether data may leave
    /// the EU must gate on this (fail-closed to EU) rather than <c>!IsEu</c> (which would
    /// fail-open: a failed/unresolved lookup is not the same as a confirmed non-EU tenant).
    /// </summary>
    internal static bool IsConfirmedGlobal(string? dataBoundary) =>
        string.Equals(Normalize(dataBoundary), Global, StringComparison.OrdinalIgnoreCase);
}