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
    protected int RgCounter = 0;
    protected int VmCounter = 0;
    protected string SubscriptionId { get; set; } = "00000000-0000-0000-0000-000000000000";

    /// <summary>
    /// On-disk profile store backing the real <see cref="ProfileService"/> used by
    /// benchmarks. Seeded once per process by <see cref="InitializeCommon"/>.
    /// </summary>
    protected BenchmarkProfileStore ProfileStore { get; } = new();

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
    /// Initialize repo root and seed the benchmark profile store (one-time setup).
    /// </summary>
    /// <remarks>
    /// Seeding happens here rather than in a new <c>[GlobalSetup]</c> because
    /// BenchmarkDotNet permits only one <c>[GlobalSetup]</c> per type hierarchy and both
    /// <c>CLIBenchmarkBase</c> and <c>PSBenchmarkBase</c> already declare one. Both call
    /// this method from theirs.
    /// </remarks>
    protected void InitializeCommon()
    {
        RepoRoot = RepoLocator.GetRepoRoot();
        ProfileStore.Initialize(RepoRoot);
    }

    /// <summary>
    /// Registers DI overrides for benchmark isolation.
    /// Call before each iteration since <see cref="AzclipsServiceProvider.Dispose"/>
    /// clears all registrations.
    /// </summary>
    /// <remarks>
    /// HTTP, telemetry, and auth are stubbed so benchmarks never leave the machine.
    /// Profiles deliberately are not: the real <see cref="ProfileService"/> is registered
    /// against <see cref="ProfileStore"/> so the JSON read, serialize, and atomic
    /// write-plus-rename that commands such as <c>login</c> perform are actually
    /// measured. The explicit base directory keeps the run away from the developer's
    /// real <c>~/.azclips</c>.
    /// </remarks>
    protected void RegisterBenchmarkStubs()
    {
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<IHttpTerminateHandler, StubHttpTerminateHandler>();
            services.AddSingleton<ITelemetryService, StubTelemetryService>();
            services.AddSingleton<IProfileService>(sp =>
                new ProfileService(sp.GetRequiredService<ILoggingService>(), ProfileStore.RootDirectory));
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
}
