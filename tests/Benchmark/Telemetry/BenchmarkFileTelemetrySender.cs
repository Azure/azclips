// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Benchmark.Telemetry;

/// <summary>
/// The <see cref="ITelemetrySender"/> PowerShell benchmarks emit telemetry through. Writes trace
/// files via the production FileExporter.
/// </summary>
/// <remarks>
/// Benchmark-only, supplied by PSBenchmarkBase.
///
/// <para>The processors run inside the measured region. That cost is accepted: they only latch a
/// flag and enqueue the activity, and the batch processor drains the queue to disk on shutdown in
/// [IterationCleanup].</para>
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
