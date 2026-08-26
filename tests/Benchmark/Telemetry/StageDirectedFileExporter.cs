// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Telemetry;
using OpenTelemetry;

namespace Benchmark.Telemetry;

/// <summary>
/// Writes one benchmark iteration's spans into either the warmup or the measured directory, chosen
/// by the flag <see cref="StageLatch"/> sets while that iteration runs.
/// </summary>
internal sealed class StageDirectedFileExporter : BaseExporter<Activity>
{
    private readonly string _warmupDirectory;
    private readonly string _actualDirectory;

    // Written on the cmdlet thread by StageLatch, read on the batch worker thread.
    private volatile bool _measured;

    private FileExporter? _inner;

    internal StageDirectedFileExporter(string warmupDirectory, string actualDirectory)
    {
        _warmupDirectory = warmupDirectory;
        _actualDirectory = actualDirectory;
    }

    /// <summary>
    /// Records that this iteration is a measured one. Set-only: one span observed inside the
    /// measured region proves the whole iteration is measured, and a span ending outside it must
    /// not undo that.
    /// </summary>
    internal void MarkMeasured() => _measured = true;

    public override ExportResult Export(in Batch<Activity> batch) =>
        (_inner ??= new FileExporter(_measured ? _actualDirectory : _warmupDirectory)).Export(batch);
}
