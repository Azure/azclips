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
    public void ConfigureExporter(TracerProviderBuilder builder)
    {
        // dataBoundary: null selects the dynamic-routing AriaActivityExporter, which resolves
        // the destination per exported Activity instead of freezing it at construction time —
        // required here because this sender builds one exporter that outlives many command
        // invocations (e.g. PowerShell's singleton TracerProvider), each of which may carry a
        // different tenant/boundary.
        TelemetryExporters.AddTraceExporters(
            builder,
            TelemetryTargetResolver.Resolve(),
            dataBoundary: null);
    }
}
