// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// Telemetry sender for PowerShell/Agent: exports traces inline via the
/// TracerProvider pipeline. The exporter is chosen by the shared target seam
/// (local OTLP → Jaeger, or remote Azure Monitor). No file buffering or replay.
/// </summary>
public class DirectTelemetrySender : ITelemetrySender
{
    public void ConfigureExporter(TracerProviderBuilder builder, string traceDirectory)
    {
        TelemetryExporters.AddTraceExporter(builder, TelemetryTargetResolver.Resolve());
    }

    /// <summary>
    /// No-op: telemetry is already exported inline by the TracerProvider.
    /// </summary>
    public Task SendPendingAsync() => Task.CompletedTask;
}
