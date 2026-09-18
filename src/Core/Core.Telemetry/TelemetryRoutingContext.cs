// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry;

/// <summary>
/// Per-invocation routing metadata (tenant/data-boundary) stamped onto trace entries so
/// the detached uploader can route buffered telemetry without re-resolving the user's
/// active profile.
/// </summary>
/// <remarks>
/// Implementations must be safe under concurrent command execution — the engine and
/// PowerShell host can run multiple invocations against the same process, so this is
/// deliberately not a plain shared field. See <see cref="TelemetryRoutingContext"/>,
/// which backs it with <see cref="AsyncLocal{T}"/> so each logical call flow keeps its
/// own value instead of clobbering another invocation's.
/// </remarks>
public interface ITelemetryRoutingContext
{
    /// <summary>Sets the routing metadata for the current async flow.</summary>
    void Set(string? tenantId, string? dataBoundary);

    /// <summary>Gets the routing metadata for the current async flow.</summary>
    (string? TenantId, string? DataBoundary) Current { get; }
}

/// <summary>
/// <see cref="AsyncLocal{T}"/>-backed implementation of <see cref="ITelemetryRoutingContext"/>.
/// Each logical call flow (including concurrent engine sessions and PowerShell cmdlet
/// invocations sharing one process) sees only the value it set, never another
/// invocation's — unlike a process-global static field.
/// </summary>
internal sealed class TelemetryRoutingContext : ITelemetryRoutingContext
{
    private readonly AsyncLocal<(string? TenantId, string? DataBoundary)> _state = new();

    public void Set(string? tenantId, string? dataBoundary)
    {
        _state.Value = (
            string.IsNullOrWhiteSpace(tenantId) ? null : tenantId,
            string.IsNullOrWhiteSpace(dataBoundary) ? null : DataBoundaryRouting.Normalize(dataBoundary));
    }

    public (string? TenantId, string? DataBoundary) Current => _state.Value;
}