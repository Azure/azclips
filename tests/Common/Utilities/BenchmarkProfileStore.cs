// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Runtime;
using Tests.Common.Stubs;

namespace Tests.Common.Utilities;

/// <summary>
/// Owns the isolated, file-backed profile store the benchmarks run against.
/// <para>
/// Benchmarks use the real <see cref="ProfileService"/> so their timings include genuine profile
/// file IO. The store is deliberately kept out of the developer's real
/// <c>{LocalApplicationData}/.azclips</c>: redirecting that via environment variables does not work
/// in-process on Windows, because .NET resolves <see cref="Environment.SpecialFolder.LocalApplicationData"/>
/// through the Win32 shell API rather than <c>%LOCALAPPDATA%</c>.
/// </para>
/// </summary>
public static class BenchmarkProfileStore
{
    /// <summary>Name of the single seeded profile. Matches the profile <c>StubProfileService</c> seeded.</summary>
    public const string ProfileName = "default";

    public const string BenchmarkTenantId = "00000000-0000-0000-0000-000000000000";

    public const string BenchmarkSubscriptionId = "00000000-0000-0000-0000-000000000000";

    private const string ProfilesFileName = "profiles.json";
    private const string ActiveProfileFileName = "active-profile";

    /// <summary>
    /// Store location: a sibling of the existing benchmark extensions home
    /// (<c>artifacts/Benchmark/home/extensions</c>), so the whole benchmark home is one tree.
    /// </summary>
    public static string GetRoot(string repoRoot) =>
        Path.Combine(repoRoot, "artifacts", "Benchmark", "home", ".azclips");

    /// <summary>
    /// Resets the store to exactly one active <see cref="ProfileName"/> profile, discarding anything
    /// a previous iteration wrote. Writes go through the production <see cref="ProfileService"/> so
    /// the on-disk bytes are exactly what a real user's store contains.
    /// </summary>
    public static async Task SeedAsync(string root)
    {
        Directory.CreateDirectory(root);
        DeleteIfExists(Path.Combine(root, ProfilesFileName));
        DeleteIfExists(Path.Combine(root, ActiveProfileFileName));

        var service = new ProfileService(new NullLoggingService(), root);
        await service.SaveAndActivateProfileAsync(CreateSeedProfile());
    }

    /// <summary>
    /// Throws unless the store resolves to the seeded active profile. Without this guard a failed
    /// seed would leave <c>Engine.ResolveProfileAsync</c> short-circuiting on a blank active-profile
    /// name, silently benchmarking a profile-less path that still exits 0.
    /// </summary>
    public static async Task VerifySeededAsync(string root)
    {
        var service = new ProfileService(new NullLoggingService(), root);
        var active = await service.GetActiveProfileAsync();

        if (active is null || !string.Equals(active.Name, ProfileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Benchmark profile store at '{root}' did not resolve the seeded '{ProfileName}' profile " +
                $"(resolved: '{active?.Name ?? "<null>"}'). Benchmarks would measure a profile-less path.");
        }
    }

    /// <summary>Best-effort removal of the store. Never throws.</summary>
    public static void Cleanup(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch (IOException) { /* best-effort */ }
        catch (UnauthorizedAccessException) { /* best-effort */ }
    }

    private static Profile CreateSeedProfile() => new()
    {
        Name = ProfileName,
        TenantId = BenchmarkTenantId,
        SubscriptionId = BenchmarkSubscriptionId,
        CloudName = "AzureCloud",
        AuthMethod = AuthMethod.Browser,
    };

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
