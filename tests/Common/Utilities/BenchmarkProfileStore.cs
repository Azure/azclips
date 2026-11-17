// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Runtime;
using Core.Runtime.Persistence;
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

    /// <summary>Attempts to remove a store file before a transient lock is treated as a real failure.</summary>
    private const int DeleteRetries = 5;

    private const int DeleteRetryDelayMilliseconds = 20;

    /// <summary>
    /// Store location: a sibling of the existing benchmark extensions home
    /// (<c>artifacts/Benchmark/home/extensions</c>), so the whole benchmark home is one tree.
    /// </summary>
    public static string GetRoot(string repoRoot) =>
        Path.Combine(repoRoot, "artifacts", "Benchmark", "home", ".azclips");

    /// <summary>
    /// Seeds the store with a single active <see cref="ProfileName"/> profile.
    /// </summary>
    /// <param name="root">Directory to seed.</param>
    /// <param name="userPrincipalName">
    /// UPN stamped on the seeded profile. Callers exercising the real <c>MsalAuthService</c> supply
    /// the UPN of the account in their MSAL cache fixture, which account selection matches on.
    /// </param>
    public static async Task SeedAsync(string root, string? userPrincipalName = null)
    {
        Directory.CreateDirectory(root);
        DeleteIfExists(Path.Combine(root, ProfilesFileName));
        DeleteIfExists(Path.Combine(root, ActiveProfileFileName));

        var service = new ProfileService(new NullLoggingService(), new FilePersistenceStore(root));
        await service.SaveAndActivateProfileAsync(CreateSeedProfile(userPrincipalName));
    }

    /// <summary>
    /// A single shot verification that the store resolves the seeded <see cref="ProfileName"/> profile.
    /// Throws if the store is missing, resolves a different profile, or — when
    /// <paramref name="expectedUserPrincipalName"/> is supplied — carries a different UPN.
    /// </summary>
    /// <param name="root">Directory to verify.</param>
    /// <param name="expectedUserPrincipalName">
    /// When supplied, the UPN the seeded profile must carry. Surfaces a mismatch as a profile-store
    /// failure rather than an MSAL <c>no_matching_account</c> error raised later.
    /// </param>
    public static async Task VerifySeededAsync(string root, string? expectedUserPrincipalName = null)
    {
        var service = new ProfileService(new NullLoggingService(), new FilePersistenceStore(root));
        var active = await service.GetActiveProfileAsync();

        if (active is null || !string.Equals(active.Name, ProfileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Benchmark profile store at '{root}' did not resolve the seeded '{ProfileName}' profile " +
                $"(resolved: '{active?.Name ?? "<null>"}'). Benchmarks would measure a profile-less path.");
        }

        if (!string.IsNullOrWhiteSpace(expectedUserPrincipalName) &&
            !string.Equals(active.UserPrincipalName, expectedUserPrincipalName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Benchmark profile store at '{root}' seeded user principal name " +
                $"'{active.UserPrincipalName ?? "<null>"}' but '{expectedUserPrincipalName}' was expected. " +
                "MSAL account selection matches on the UPN, so a mismatch makes every silent token " +
                "acquisition fail with 'no_matching_account'.");
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

    private static Profile CreateSeedProfile(string? userPrincipalName) => new()
    {
        Name = ProfileName,
        TenantId = BenchmarkTenantId,
        SubscriptionId = BenchmarkSubscriptionId,
        CloudName = "AzureCloud",
        AuthMethod = AuthMethod.Browser,
        UserPrincipalName = userPrincipalName,
    };

    private static void DeleteIfExists(string path)
    {
        for (var attempt = 0; attempt < DeleteRetries; attempt++)
        {
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException)
            {
                // A scanner or indexer holding the file makes Delete fail on Windows, where a file
                // with an open handle cannot be removed. Those handles are released in milliseconds,
                // so back off briefly rather than failing the caller.
                Thread.Sleep(DeleteRetryDelayMilliseconds);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(DeleteRetryDelayMilliseconds);
            }
        }

        // Out of attempts: let a genuine, non-transient failure surface.
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
