// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Telemetry;

namespace Benchmark.Telemetry;

/// <summary>
/// Chooses between the warmup and measured trace directories at the moment a trace is written.
/// </summary>
/// <remarks>
/// Called from <c>FileTelemetrySender.ConfigureExporter</c>, inside the bracket BenchmarkDotNet
/// raises around each iteration's measured region, and costs one volatile bool read.
/// </remarks>
internal sealed class StageTraceDirectoryProvider : ITraceDirectoryProvider
{
    private readonly string _warmupDirectory;
    private readonly string _actualDirectory;
    private readonly BenchmarkStageListener _stage;

    internal StageTraceDirectoryProvider(
        string warmupDirectory,
        string actualDirectory,
        BenchmarkStageListener stage)
    {
        _warmupDirectory = warmupDirectory;
        _actualDirectory = actualDirectory;
        _stage = stage;
    }

    public string GetTraceDirectory() => _stage.IsMeasured ? _actualDirectory : _warmupDirectory;
}
