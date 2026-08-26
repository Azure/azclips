// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using OpenTelemetry;

namespace Benchmark.Telemetry;

/// <summary>
/// Records whether the iteration that produced a span is a measured one, so
/// <see cref="StageDirectedFileExporter"/> can pick a directory after BenchmarkDotNet has closed
/// the measured region.
/// </summary>
internal sealed class StageLatch : BaseProcessor<Activity>
{
    private readonly BenchmarkStageListener _stage;
    private readonly StageDirectedFileExporter _destination;

    internal StageLatch(BenchmarkStageListener stage, StageDirectedFileExporter destination)
    {
        _stage = stage;
        _destination = destination;
    }

    public override void OnEnd(Activity data)
    {
        // Unrecorded spans are dropped downstream, so they get no vote on the iteration's stage.
        // ImportTraceFilter runs earlier in the chain and clears Recorded on the module import
        // trace, which is emitted from a background thread whose timing is not controlled.
        if (data is null || !data.Recorded) return;

        if (_stage.IsMeasured) _destination.MarkMeasured();
    }
}
