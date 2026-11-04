// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// Entry point for the detached <c>--upload-telemetry</c> process. The uploader
/// replays routing metadata already persisted with each trace entry.
/// </summary>
public static class TelemetryUpload
{
    /// <summary>
    /// Sends all pending trace files, then deletes the ones that were delivered.
    /// </summary>
    public static void RunPending(ITraceDirectoryProvider traceDirectory)
    {
        new TraceUploader(
            new TraceFileStore(traceDirectory.GetTraceDirectory()),
            new TracerProviderExportSink(TelemetryTargetResolver.Resolve()))
        .Run();
    }
}
