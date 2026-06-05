// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;
using Core.Telemetry.Constants;

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
        _telemetryProvider.ActivitySource.StartActivity(name, kind)?.SetTag(TelemetryTags.EventName, name);

    public Activity? AddTag(string propertyName, object value, Activity? activity = null)
    {
        if (activity is not null)
        {
            activity.SetTag(propertyName, value);
            return activity;
        }
        var root = Activity.Current;
        while (root?.Parent is not null)
            root = root.Parent;

        root?.SetTag(propertyName, value);
        return root;
    }

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
