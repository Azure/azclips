// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;

namespace Core.Telemetry;

public class TelemetryService : ITelemetryService
{
    private readonly ITelemetryProvider _telemetryProvider;
    private bool _disposed;

    public TelemetryService(ITelemetryProvider telemetryProvider)
    {
        _telemetryProvider = telemetryProvider;
    }

    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal) =>
        _telemetryProvider.ActivitySource.StartActivity(name, kind);

    public void Dispose()
    {
        if (_disposed) return;

        // Skip ForceFlush — FileExporter uses SimpleActivityExportProcessor
        // which exports synchronously inline, so there's nothing to flush.
        // TracerProvider.Dispose() also tries to flush; run it on a thread
        // with a short timeout to avoid blocking on unreachable exporters
        var disposeTask = Task.Run(() => _telemetryProvider.TracerProvider.Dispose());
        disposeTask.Wait(TimeSpan.FromMilliseconds(500));

        // ActivitySource lifetime is managed by the DI container; don't dispose it here.
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
