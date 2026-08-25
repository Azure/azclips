// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Benchmark.Telemetry;

/// <summary>Locations benchmark telemetry is staged in before the host collects it.</summary>
internal static class BenchmarkTelemetryPaths
{
    /// <summary>
    /// Where each benchmark process writes its traces, and where the host collects them from.
    /// </summary>
    /// <remarks>
    /// Deliberately a sibling of <c>artifacts/Benchmark</c> rather than a child. Creating files
    /// inside that tree triggers on-access scanners to walk it, and a scanner holding
    /// <c>artifacts/Benchmark/home/.azclips/profiles.json</c> makes the per-iteration reseed fail
    /// on Windows, where a file with an open handle cannot be deleted.
    /// </remarks>
    internal static string StagingRoot(string repoRoot) =>
        Path.Combine(repoRoot, "artifacts", "BenchmarkTelemetry");
}
