// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Benchmark.Auth;
using Benchmark.Telemetry;
using Core.Abstractions;
using Core.Abstractions.Runtime.Handler;
using Core.Authentication;
using Core.DependencyInjection;
using Core.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Tests.Common.Stubs;
using Tests.Common.Utilities;

namespace Benchmark;

/// <summary>
/// Base class providing common setup for all benchmarks.
/// Handles stub HTTP setup, repo root discovery, and utility methods.
/// </summary>
public abstract class BenchmarkBase
{
    protected string RepoRoot { get; set; } = null!;
    protected string SubscriptionId { get; set; } = "00000000-0000-0000-0000-000000000000";

    /// <summary>Scratch directory holding the seeded MSAL token cache for this run.</summary>
    protected string MsalCacheDirectory { get; private set; } = null!;

    /// <summary>
    /// Tracks whether the BenchmarkDotNet engine is inside the measured stage, so traces can be
    /// separated from the warmup ones.
    /// </summary>
    private protected BenchmarkStageListener Stage { get; private set; } = null!;

    /// <summary>This benchmark method's telemetry staging directories and post-run pruning.</summary>
    private protected BenchmarkTelemetryArtifacts TelemetryArtifacts { get; private set; } = null!;

    /// <summary>
    /// The extension this benchmark class targets, read from its
    /// <see cref="BenchmarkExtensionAttribute"/>. This is the single source of truth for
    /// both CLI extension staging and PS module staging, so a benchmark only declares
    /// <c>[BenchmarkExtension("Name")]</c>. Discovery already guarantees the attribute is
    /// present, so a missing one here is a programming error.
    /// </summary>
    protected string ExtensionName
    {
        get
        {
            // BenchmarkDotNet runs a generated subclass of the benchmark type at runtime, and
            // the attribute is [AttributeUsage(Inherited = false)], so walk up from the concrete
            // runtime type to find [BenchmarkExtension] declared on the real benchmark class.
            for (Type? type = GetType(); type is not null; type = type.BaseType)
            {
                if (type.GetCustomAttribute<BenchmarkExtensionAttribute>(inherit: false) is { } attribute)
                {
                    return attribute.Name;
                }
            }

            throw new InvalidOperationException(
                $"{GetType().Name} is missing [BenchmarkExtension]; it cannot resolve its extension.");
        }
    }

    /// <summary>
    /// The isolated, file-backed profile store the benchmarks resolve profiles from.
    /// </summary>
    protected string ProfileStoreRoot { get; private set; } = null!;

    /// <summary>
    /// Initialize repo root and the seeded profile store (one-time setup).
    /// </summary>
    protected void InitializeCommon()
    {
        RepoRoot = RepoLocator.GetRepoRoot();

        MsalCacheDirectory = Path.Combine(RepoRoot, "artifacts", "Benchmark", "home", "msal");
        MsalCacheFixture.Seed(MsalCacheDirectory);
        ProfileStoreRoot = BenchmarkProfileStore.GetRoot(RepoRoot);

        // The profile store is seeded and verified once during global setup to confirm that
        // profiles are present. Executing without a profile is a supported workflow, but it is not
        // the primary one and is therefore outside the scope of these benchmarks, if they were to fail gracefully.
        SeedProfileStore();
        BenchmarkProfileStore.VerifySeededAsync(ProfileStoreRoot, MsalCacheFixture.UserPrincipalName)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    /// Resets the profile store to its seeded state. Called from each iteration's setup so every
    /// iteration starts from identical bytes even when a benchmark writes profiles.
    /// </summary>
    /// <remarks>
    /// Seeds the MSAL fixture's UPN, which <c>MsalAccountSelector</c> matches the cached account by.
    /// </remarks>
    protected void SeedProfileStore() =>
        BenchmarkProfileStore.SeedAsync(ProfileStoreRoot, MsalCacheFixture.UserPrincipalName)
            .GetAwaiter()
            .GetResult();

    /// <summary>
    /// Resolves this benchmark method's telemetry directories and starts tracking the engine's
    /// stage. Both live for the whole run, so this belongs in <c>[GlobalSetup]</c>.
    /// </summary>
    private protected void InitializeTelemetryCommon()
    {
        TelemetryArtifacts = new BenchmarkTelemetryArtifacts(RepoRoot);
        Stage = new BenchmarkStageListener();
    }

    /// <summary>
    /// Stops stage tracking and turns the measured traces into the run's deliverable. Belongs in
    /// <c>[GlobalCleanup]</c>. Null-safe, so a benchmark that failed during setup still cleans up.
    /// </summary>
    private protected void CleanupTelemetryCommon()
    {
        Stage?.Dispose();
        TelemetryArtifacts?.Prune();
    }

    /// <summary>
    /// Registers stub DI overrides for benchmark isolation.
    /// Call before each iteration since <see cref="AzclipsServiceProvider.Dispose"/>
    /// clears all registrations.
    /// </summary>
    protected void RegisterBenchmarkStubs()
    {
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<IHttpTerminateHandler, StubHttpTerminateHandler>();
            services.AddSingleton<ITelemetryService, StubTelemetryService>();
            services.AddSingleton<IProfileService>(sp =>
                new ProfileService(sp.GetRequiredService<ILoggingService>(), ProfileStoreRoot));
            services.AddSingleton(new MsalClientOptions
            {
                CacheDirectory = MsalCacheDirectory,
                HttpClientFactory = new ThrowingMsalHttpClientFactory(),
            });
        });
    }

    /// <summary>
    /// Proves, once per run, that the seeded cache yields a token without touching the network.
    /// Mirrors exactly what an iteration does. A drifted fixture, a UPN mismatch, or a stubbed-out
    /// auth service would otherwise change what is being measured without changing the numbers
    /// enough to notice — this turns that into a hard setup failure.
    /// </summary>
    protected void VerifyAuthFixture()
    {
        RegisterBenchmarkStubs();
        AzclipsServiceProvider.Initialize(ClientType.CLI);
        try
        {
            var authService = AzclipsServiceProvider.GetRequiredService<IAuthService>();
            if (authService is not MsalAuthService)
            {
                throw new InvalidOperationException(
                    $"Benchmarks must exercise the real MsalAuthService but resolved " +
                    $"'{authService.GetType().Name}'. Auth must not be stubbed.");
            }

            var result = authService
                .AcquireTokenSilentlyAsync(scopes: null, tenantId: null)
                .GetAwaiter()
                .GetResult();

            if (!string.Equals(result.AccessToken, MsalCacheFixture.AccessTokenSecret, StringComparison.Ordinal) ||
                !string.Equals(result.UserPrincipalName, MsalCacheFixture.UserPrincipalName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Silent acquisition returned a token that does not match the seeded cache. " +
                    $"Expected UPN '{MsalCacheFixture.UserPrincipalName}', got '{result.UserPrincipalName}'.");
            }

            Console.WriteLine("[Setup] MSAL cache hit verified — auth file IO is measured, network is blocked");
        }
        catch (Exception ex)
        {
            var cacheFile = Path.Combine(MsalCacheDirectory, "MSAL.cache.plaintext");
            var exists = File.Exists(cacheFile);
            var size = exists ? new FileInfo(cacheFile).Length : 0;
            throw new InvalidOperationException(
                $"MSAL benchmark fixture verification failed. Cache file '{cacheFile}' " +
                $"exists={exists}, size={size} bytes. The benchmark cannot run without a cache hit, " +
                "because a miss would either fail or measure something other than file IO.", ex);
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
        }
    }

    /// <summary>
    /// Cleanup: dispose DI container (also clears registrations).
    /// </summary>
    protected void CleanupCommon()
    {
        AzclipsServiceProvider.Dispose();
    }

    /// <summary>
    /// One-time cleanup: remove the benchmark profile store. Best-effort.
    /// </summary>
    protected void CleanupGlobal() => BenchmarkProfileStore.Cleanup(ProfileStoreRoot);
}
