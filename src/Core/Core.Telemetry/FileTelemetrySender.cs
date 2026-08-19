// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// Buffers traces to local files. A detached process replays them via
/// <see cref="TelemetryUpload"/>.
/// </summary>
public class FileTelemetrySender : ITelemetrySender
{
    public void ConfigureExporter(TracerProviderBuilder builder)
    {
        builder.AddProcessor(
            new SimpleActivityExportProcessor(
                new FileExporter(TraceFileStore.DefaultDirectory)));
    }
}
