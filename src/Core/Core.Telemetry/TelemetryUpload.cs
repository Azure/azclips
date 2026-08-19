// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry;

/// <summary>
/// Entry point for the detached <c>--upload-telemetry</c> process. Runs before the DI
/// container exists, so it wires the uploader by hand.
/// </summary>
public static class TelemetryUpload
{
    /// <summary>The directory where trace files are written and read from.</summary>
    public static string TraceDirectory => TraceFileStore.DefaultDirectory;

    /// <summary>Sends all pending trace files, then deletes the ones that were delivered.</summary>
    public static void RunPending() =>
        new TraceUploader(
            new TraceFileStore(TraceFileStore.DefaultDirectory),
            new TracerProviderExportSink(TelemetryTargetResolver.Resolve()))
        .Run();
}
