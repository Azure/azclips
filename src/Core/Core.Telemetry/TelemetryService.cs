using System;
using System.Diagnostics;
using Core.Abstractions;

namespace Core.Telemetry;

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
