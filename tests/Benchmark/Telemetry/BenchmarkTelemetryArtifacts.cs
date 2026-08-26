// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Tests.Common.Utilities;

namespace Benchmark.Telemetry;

/// <summary>
/// Owns one benchmark method's telemetry staging directories and the post-run pruning that turns
/// them into the run's deliverable. Shared by the CLI and PowerShell benchmark bases.
/// </summary>
/// <remarks>
/// Benchmark-only.
///
/// <para><b>Never runs inside the measured region.</b> Constructed in [GlobalSetup] and used from
/// [GlobalCleanup], neither of which BenchmarkDotNet measures.</para>
/// </remarks>
internal sealed class BenchmarkTelemetryArtifacts
{
    private readonly string _stagingRoot;
    private readonly string _telemetryRoot;
    private readonly string _benchmarkName;

    /// <summary>
    /// Resolves this process's benchmark method and its directories, then clears the root. The
    /// staging path is stable across runs, so that clear is what keeps runs separate.
    /// </summary>
    internal BenchmarkTelemetryArtifacts(string repoRoot)
    {
        // One process per benchmark case, so this is constant for the whole run.
        _benchmarkName = BenchmarkName.FromCommandLine();
        _stagingRoot = StagingRoot(repoRoot);
        _telemetryRoot = Path.Combine(_stagingRoot, _benchmarkName);

        DirectoryUtils.TryDeleteDirectory(_telemetryRoot);
    }

    /// <summary>
    /// Where every benchmark process stages its traces, and where the host collects them from.
    /// </summary>
    /// <remarks>
    /// A sibling of <c>artifacts/Benchmark</c> rather than a child, and it must stay one. Creating
    /// files inside that tree makes on-access scanners walk it, and a scanner holding
    /// <c>artifacts/Benchmark/home/.azclips/profiles.json</c> open breaks the per-iteration profile
    /// reseed on Windows, where a file with an open handle cannot be deleted.
    /// </remarks>
    internal static string StagingRoot(string repoRoot) =>
        Path.Combine(repoRoot, "artifacts", "BenchmarkTelemetry");

    /// <summary>Where warmup-iteration traces are written. Deleted unread by <see cref="Prune"/>.</summary>
    internal string WarmupDirectory => Path.Combine(_telemetryRoot, "warmup");

    /// <summary>Where measured-iteration traces are written. The only directory consumed.</summary>
    internal string ActualDirectory => Path.Combine(_telemetryRoot, "actual");

    /// <summary>
    /// Samples the measured traces into the run's output file and removes the raw ones. Never
    /// throws: a telemetry failure must not change the benchmark's exit code.
    /// </summary>
    internal void Prune()
    {
        try
        {
            var kept = TraceSampler.Prune(
                ActualDirectory,
                Path.Combine(_stagingRoot, _benchmarkName + ".jsonl"));

            if (kept == 0)
            {
                Console.WriteLine(
                    $"[Telemetry] {_benchmarkName}: no traces captured. Either telemetry is " +
                    "disabled in ~/.azclips/config.json, or no measured iteration ran.");
            }
            else
            {
                Console.WriteLine($"[Telemetry] {_benchmarkName}: kept {kept} sampled trace(s)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Telemetry] {_benchmarkName}: prune failed ({ex.Message})");
        }
        finally
        {
            DirectoryUtils.TryDeleteDirectory(_telemetryRoot);
        }
    }
}
