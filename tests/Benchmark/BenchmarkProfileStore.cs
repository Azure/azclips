// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Runtime;

namespace Benchmark;

/// <summary>
/// Owns the benchmark-scoped profile store on disk so benchmarks exercise the real
/// file-backed <see cref="ProfileService"/> rather than an in-memory stub. Profile
/// persistence is a genuine cost of commands such as <c>login</c> — a JSON read, a
/// serialize, and two atomic temp-write-plus-rename cycles — and stubbing it out hid
/// that cost entirely.
/// </summary>
/// <remarks>
/// The directory is suffixed with the current process id. BenchmarkDotNet runs each
/// benchmark case in its own child process, so this guarantees a clean seeded store per
/// process and makes it impossible for two concurrent benchmark invocations to collide
/// on the same files. It lives under the existing <c>artifacts/Benchmark/home</c> tree
/// that <c>CLIBenchmarkBase</c> already creates and removes.
/// </remarks>
public sealed class BenchmarkProfileStore
{
    /// <summary>
    /// Seeded into every benchmark process so each measured iteration reads an existing
    /// <c>profiles.json</c> rather than short-circuiting on a missing file. Mirrors the
    /// profile <c>StubProfileService</c> seeds, preserving the connection state
    /// benchmarks previously saw.
    /// </summary>
    private static readonly Profile SeedProfile = new()
    {
        Name = "default",
        TenantId = "00000000-0000-0000-0000-000000000000",
        SubscriptionId = "00000000-0000-0000-0000-000000000000",
        CloudName = "AzureCloud",
        AuthMethod = AuthMethod.Browser,
    };

    /// <summary>
    /// Absolute path passed to <see cref="ProfileService"/> as its base directory.
    /// Empty until <see cref="Initialize"/> runs.
    /// </summary>
    public string RootDirectory { get; private set; } = string.Empty;

    /// <summary>
    /// Resolves the directory, resets it, and seeds it. Call once per process from
    /// <c>[GlobalSetup]</c> so the seeding I/O stays outside the measured region.
    /// </summary>
    public void Initialize(string repoRoot)
    {
        RootDirectory = Path.Combine(
            repoRoot, "artifacts", "Benchmark", "home", $".azclips-{Environment.ProcessId}");

        Delete();
        Directory.CreateDirectory(RootDirectory);

        // Seed through the real service rather than hand-written JSON so the on-disk
        // format always matches production and the serializer options stay in one place.
        var seeder = new ProfileService(new SilentLoggingService(), RootDirectory);
        seeder.SaveAndActivateProfileAsync(SeedProfile).GetAwaiter().GetResult();

        Console.WriteLine($"[Setup] Benchmark profile store seeded at {RootDirectory}");
    }

    /// <summary>Removes the store, best-effort.</summary>
    public void Delete()
    {
        if (string.IsNullOrEmpty(RootDirectory))
        {
            return;
        }

        try
        {
            if (Directory.Exists(RootDirectory))
            {
                Directory.Delete(RootDirectory, recursive: true);
            }
        }
        catch (IOException) { /* best-effort */ }
        catch (UnauthorizedAccessException) { /* best-effort */ }
    }

    /// <summary>
    /// Discards seeding log output. <see cref="ProfileService"/> writes a Verbose line on
    /// every load and save, and with no active logging scope <c>ScopedLoggingService</c>
    /// falls back to <c>ConsoleLoggingService</c>, which writes to stderr.
    /// </summary>
    private sealed class SilentLoggingService : ILoggingService
    {
        public void Log(string message) { }
        public void Info(string message) { }
        public void Verbose(string message) { }
        public void Debug(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
