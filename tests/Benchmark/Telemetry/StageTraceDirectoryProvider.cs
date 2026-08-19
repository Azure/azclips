// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Telemetry;

namespace Benchmark.Telemetry;

/// <summary>
/// Chooses between the warmup and measured trace directories at the moment a trace is written.
/// </summary>
/// <remarks>
/// The choice cannot be made in <c>[IterationSetup]</c>. BenchmarkDotNet raises
/// <c>WorkloadActualStart</c>/<c>WorkloadActualStop</c> around the measured region of each
/// iteration, and <c>[IterationSetup]</c> runs outside that bracket, so it always observes
/// <c>IsMeasured == false</c> — including on measured iterations. <see cref="GetTraceDirectory"/>
/// is called while <c>CLITelemetryProvider</c> is being constructed inside the benchmarked call,
/// which is inside the bracket, so it observes the true stage.
///
/// Both paths are precomputed by the caller, so the measured region pays only a volatile bool read
/// and a reference return — identical work in either stage, so it cannot skew the distribution.
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
