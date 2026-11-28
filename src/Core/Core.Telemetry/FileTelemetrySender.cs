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
    private readonly bool _bypassRules;

    public FileTelemetrySender(ITraceDirectoryProvider traceDirectory)
        : this(traceDirectory, TelemetryTargetResolver.BypassesRules)
    {
    }

    internal FileTelemetrySender(ITraceDirectoryProvider traceDirectory, bool bypassRules)
    {
        _traceDirectory = traceDirectory;
        _bypassRules = bypassRules;
    }

    public void ConfigureExporter(TracerProviderBuilder builder)
    {
        // Resolved per export, so the destination can differ between exports from one sender.
        builder.AddProcessor(
            new SimpleActivityExportProcessor(
                new FileExporter(_traceDirectory.GetTraceDirectory(), keepAllData: _bypassRules)));
    }
}
