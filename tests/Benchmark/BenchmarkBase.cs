// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Core.Abstractions;
using Core.Abstractions.Runtime.Handler;
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
        ProfileStoreRoot = BenchmarkProfileStore.GetRoot(RepoRoot);

        // The profile store is seeded and verified once during global setup to confirm that
        // profiles are present. Executing without a profile is a supported workflow, but it is not
        // the primary one and is therefore outside the scope of these benchmarks, if they were to fail gracefully.
        SeedProfileStore();
        BenchmarkProfileStore.VerifySeededAsync(ProfileStoreRoot).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Resets the profile store to its seeded state. Called from each iteration's setup so every
    /// iteration starts from identical bytes even when a benchmark writes profiles.
    /// </summary>
    protected void SeedProfileStore() =>
        BenchmarkProfileStore.SeedAsync(ProfileStoreRoot).GetAwaiter().GetResult();

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
            services.AddSingleton<IAuthService, StubAuthService>();
        });
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
