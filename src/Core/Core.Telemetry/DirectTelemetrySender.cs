// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// Exports traces inline via the TracerProvider pipeline, one exporter per resolved
/// target. No file buffering or replay.
/// </summary>
public class DirectTelemetrySender : ITelemetrySender
{
    private readonly ITelemetryRoutingContext? _routingContext;

    public DirectTelemetrySender(ITelemetryRoutingContext? routingContext = null)
    {
        _routingContext = routingContext;
    }

    public void ConfigureExporter(TracerProviderBuilder builder)
    {
        // liveRoutingContext lets the Aria exporter re-check the boundary per export instead
        // of freezing it at construction time — required here because this sender builds one
        // exporter that outlives many command invocations (e.g. PowerShell's singleton
        // TracerProvider), each of which may carry a different tenant/boundary.
        TelemetryExporters.AddTraceExporters(
            builder,
            TelemetryTargetResolver.Resolve(),
            dataBoundary: null,
            liveRoutingContext: _routingContext);
    }
}
