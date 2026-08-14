// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Runtime;
using Tests.Common.Stubs;

namespace Tests.Common.Utilities;

/// <summary>
/// Provides utilities for provisioning isolated profile directories and their associated artifacts,
/// enabling tests and benchmarks to exercise genuine file-backed profile paths rather than mocks.
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
    /// Seeds the store with a single active <see cref="ProfileName"/> profile.
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
    /// A single shot verification that the store resolves the seeded <see cref="ProfileName"/> profile.
    /// Throws if the store is missing or resolves a different profile.
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
