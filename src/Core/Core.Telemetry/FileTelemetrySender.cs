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
        // Resolved here rather than in the constructor so a host can decide the destination per
        // export. Benchmarks rely on that: which directory a trace belongs to is only knowable
        // once the exporter is being built, not when the sender is registered.
        builder.AddProcessor(
            new SimpleActivityExportProcessor(
                new FileExporter(_traceDirectory.GetTraceDirectory())));
    }
}
