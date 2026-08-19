// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// Telemetry sender for CLI: buffers traces to local files via FileExporter,
/// then replays them to the resolved telemetry target (local OTLP or Azure Monitor)
/// in a background process.
/// </summary>
public class FileTelemetrySender : ITelemetrySender
{
    public void ConfigureExporter(TracerProviderBuilder builder, string traceDirectory)
    {
        builder.AddProcessor(new SimpleActivityExportProcessor(new FileExporter(traceDirectory)));
    }

    // Temporary: ITelemetrySender still declares this member. Task 7 removes both.
    Task ITelemetrySender.SendPendingAsync()
    {
        TelemetryUpload.RunPending();
        return Task.CompletedTask;
    }
}
