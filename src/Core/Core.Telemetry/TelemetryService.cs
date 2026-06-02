using System;
using System.Diagnostics;
using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// Records telemetry on a per-process <see cref="ActivitySource"/> keyed by
/// <see cref="ClientType"/>. Pipeline ownership (TracerProvider, exporters,
/// instrumentation) belongs to the host (<see cref="ITelemetryHost"/>) — each entry
/// point (CLI, TUI, PowerShell) builds its own.
/// </summary>
/// <remarks>
/// Activities started via this recorder are only exported when a <c>TracerProvider</c>
/// is listening to <see cref="ActivitySource"/>. With no host attached the recorder
/// still works — activities are silently dropped.
/// </remarks>
public class TelemetryService : ITelemetryService
{
    private readonly ITelemetryProvider _telemetryProvider;

    public TelemetryService(ITelemetryProvider telemetryProvider)
    {
        _telemetryProvider = telemetryProvider;
    }

    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal) => _telemetryProvider.ActivitySource.StartActivity(name, kind);

    public void Dispose()
    {
        _telemetryProvider.TracerProvider.Dispose();
        GC.SuppressFinalize(this);
    }
}
