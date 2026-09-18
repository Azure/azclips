// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry;

/// <summary>
/// Process-local routing metadata stamped onto trace files so the detached uploader
/// can route buffered telemetry without re-resolving the user's active profile.
/// </summary>
public static class TelemetryRoutingContext
{
    public static string? TenantId { get; private set; }
    public static string? DataBoundary { get; private set; }

    public static void Set(string? tenantId, string? dataBoundary)
    {
        TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
        DataBoundary = string.IsNullOrWhiteSpace(dataBoundary)
            ? null
            : DataBoundaryRouting.Normalize(dataBoundary);
    }

    public static void Clear() => Set(null, null);
}