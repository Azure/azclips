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
        : this(persistence, TelemetryTargetResolver.BypassesRules,
            new TracerProviderExportSink(TelemetryTargetResolver.Resolve()))
    {
    }

    internal TelemetryUpload(IPersistenceStore persistence, ITraceExportSink sink)
        : this(persistence, TelemetryTargetResolver.BypassesRules, sink)
    {
    }

    /// <param name="bypassRules">
    /// Selects the local-only folder, which holds unfiltered traces. Only a process whose sole
    /// target is the local collector may pass true.
    /// </param>
    internal TelemetryUpload(IPersistenceStore persistence, bool bypassRules, ITraceExportSink sink)
    {
        _uploader = new TraceUploader(
            new TraceFileStore(persistence.GetFilePath(TelemetryTargetResolver.TraceDirectoryNameFor(bypassRules))),
            sink);
    }

    /// <summary>
    /// Sends all pending trace files, then deletes the ones that were delivered.
    /// </summary>
    public void RunPending() => _uploader.Run();
}
