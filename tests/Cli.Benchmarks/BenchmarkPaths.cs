namespace Cli.Benchmarks;

/// <summary>
/// Provides the artifacts directory path for benchmarks.
/// This path is computed at compile-time to avoid issues with BenchmarkDotNet's
/// temporary directory isolation.
/// </summary>
public static class BenchmarkPaths
{
    // This path is hard-coded to the repository root's artifacts/Debug directory.
    // Update this if the repository structure changes.
    public static readonly string ArtifactsDir = @"C:\Users\norizhang\repos\cirrus\artifacts\Debug";
}
