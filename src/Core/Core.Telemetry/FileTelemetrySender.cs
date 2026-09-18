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
    private readonly ITraceDirectoryProvider _traceDirectory;

    public FileTelemetrySender(ITraceDirectoryProvider traceDirectory)
    {
        _traceDirectory = traceDirectory;
    }

    public void ConfigureExporter(TracerProviderBuilder builder)
    {
        // Resolved per export, so the destination can differ between exports from one sender.
        builder.AddProcessor(
            new SimpleActivityExportProcessor(
                new FileExporter(_traceDirectory.GetTraceDirectory())));
    }
}
