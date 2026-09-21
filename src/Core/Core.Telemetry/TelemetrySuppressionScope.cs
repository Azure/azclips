// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry;

/// <summary>
/// Ambient (AsyncLocal-backed) scope that suppresses <see cref="TelemetryService.StartActivity"/>
/// for the duration of the call it wraps.
/// </summary>
/// <remarks>
/// Exists for "bootstrap" instrumented calls that run BEFORE the information needed to route
/// their own telemetry is known — the canonical example is the token acquisition inside
/// <c>Engine.ResolveDataBoundaryAsync</c>: it must call the (traced)
/// <c>MsalAuthService.AcquireTokenSilentlyAsync</c> to discover the ARM data boundary, but that
/// call's own span would necessarily end (and export) before the boundary is known, so
/// <c>AriaActivityExporter</c> would fall back to routing it to Global even for an EU tenant.
/// Suppressing the span entirely avoids exporting it under the wrong destination.
/// <para>
/// Safe to rely on AsyncLocal here specifically because this is a pure FORWARD-propagation use:
/// the code that calls <see cref="Enter"/> then itself calls (awaits) the instrumented method —
/// it never needs the suppression to flow backward from a callee into a caller, which is the
/// direction AsyncLocal cannot support (see ExecutionSession/TelemetryRoutingResolver remarks
/// for the fuller story on that constraint).
/// </para>
/// </remarks>
public static class TelemetrySuppressionScope
{
    private static readonly AsyncLocal<bool> Suppressed = new();

    /// <summary>Whether telemetry is currently suppressed for the ambient async flow.</summary>
    public static bool IsSuppressed => Suppressed.Value;

    /// <summary>
    /// Suppresses telemetry for the ambient async flow until the returned scope is disposed.
    /// Nesting is supported: disposing restores the previous (possibly already-suppressed) state.
    /// </summary>
    public static IDisposable Enter()
    {
        var previous = Suppressed.Value;
        Suppressed.Value = true;
        return new Scope(previous);
    }

    private sealed class Scope(bool previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Suppressed.Value = previous;
        }
    }
}
