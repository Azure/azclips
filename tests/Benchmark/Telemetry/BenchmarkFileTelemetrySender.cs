// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Benchmark.Telemetry;

/// <summary>
/// The <see cref="ITelemetrySender"/> PowerShell benchmarks run with. Writes trace files through
/// the production FileExporter, off the measured thread.
/// </summary>
/// <remarks>
/// Benchmark-only. PSBenchmarkBase supplies it via AzclipsServiceProvider.OverrideRegistration,
/// which beats the DirectTelemetrySender the PowerShell module registers in the prepend phase.
///
/// <para><b>ConfigureExporter does not run inside the measured region.</b> PSBenchmarkBase resolves
/// ITelemetryService in [IterationSetup] to force provider construction — and therefore this
/// method — before the stopwatch starts.</para>
///
/// <para>The batch processor means the timed thread only enqueues; the stage-directed exporter
/// separates warmup traces from measured ones. Everything created here is owned by the
/// TracerProvider and dies with it in [IterationCleanup], where the batch shutdown drains the queue
/// to disk.</para>
/// </remarks>
internal sealed class BenchmarkFileTelemetrySender : ITelemetrySender
{
    private readonly BenchmarkStageListener _stage;
    private readonly string _warmupDirectory;
    private readonly string _actualDirectory;

    internal BenchmarkFileTelemetrySender(
        BenchmarkStageListener stage,
        string warmupDirectory,
        string actualDirectory)
    {
        _stage = stage;
        _warmupDirectory = warmupDirectory;
        _actualDirectory = actualDirectory;
    }

    public void ConfigureExporter(TracerProviderBuilder builder)
    {
        // One exporter per iteration, so each iteration writes its own traces-<traceId>.log.
        var exporter = new StageDirectedFileExporter(_warmupDirectory, _actualDirectory);

        // Order matters: the latch reads the stage while the measured region is open, and the
        // batch processor enqueues afterwards.
        builder.AddProcessor(new StageLatch(_stage, exporter));
        builder.AddProcessor(new BatchActivityExportProcessor(exporter));
    }
}
