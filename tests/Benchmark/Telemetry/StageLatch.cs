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
/// <remarks>
/// Benchmark-only.
///
/// <para><b>Runs inside the measured region.</b> OpenTelemetry calls OnEnd on the thread that ends
/// the activity — the cmdlet thread — so this is limited to a flag check, a volatile read, and a
/// store: no allocation, no I/O, no locking.</para>
///
/// <para>Engine.RunIteration raises WorkloadActualStart immediately before the measured call and
/// WorkloadActualStop immediately after it, so BenchmarkStageListener.IsMeasured reads false in
/// [IterationSetup], where the TracerProvider is built, and false again in [IterationCleanup],
/// where the file is written. It is true only while the benchmark method runs.</para>
/// </remarks>
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
