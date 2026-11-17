// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>Ships replayed traces for one service.</summary>
internal interface ITraceExportSink
{
    /// <summary>
    /// Exports one service's traces. Takes a single service per call because a
    /// TracerProvider's Resource is immutable and supplies <c>service.name</c>.
    /// Returns true only when the batch flushed successfully.
    /// </summary>
    bool TryExport(
        string serviceName,
        IReadOnlyList<TraceEntry> entries,
        UploadRegion? uploadRegion = null,
        TraceReplayProfile profile = TraceReplayProfile.Legacy,
        TraceReplayContext? replayContext = null);
}
