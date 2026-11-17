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
        // Omit fixed metadata so long-lived providers route each Activity independently.
        TelemetryExporters.AddTraceExporters(
            builder,
            TelemetryTargetResolver.Resolve());
    }
}
