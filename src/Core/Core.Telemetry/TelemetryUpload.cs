// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// Entry point for the detached <c>--upload-telemetry</c> process. The uploader
/// replays routing metadata already persisted with each trace entry.
/// </summary>
public sealed class TelemetryUpload
{
    private readonly TraceUploader _uploader;

    public TelemetryUpload(IPersistenceStore persistence)
        : this(persistence, new TracerProviderExportSink(TelemetryTargetResolver.Resolve()))
    {
    }

    internal TelemetryUpload(IPersistenceStore persistence, ITraceExportSink sink)
    {
        _uploader = new TraceUploader(
            new TraceFileStore(persistence.GetFilePath(HomePaths.TraceDirectoryName)), sink);
    }

    /// <summary>
    /// Sends all pending trace files, then deletes the ones that were delivered.
    /// </summary>
    public void RunPending() => _uploader.Run();
}
