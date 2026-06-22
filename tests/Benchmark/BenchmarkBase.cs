// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;
using Core.Abstractions.Runtime.Handler;
using Core.DependencyInjection;
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
    protected string SubscriptionId { get; set; } = "00000000-0000-0000-0000-000000000000";

    /// <summary>
    /// Initialize repo root (one-time setup).
    /// </summary>
    protected void InitializeCommon()
    {
        RepoRoot = RepoLocator.GetRepoRoot();
    }

    /// <summary>
    /// Registers stub DI overrides for benchmark isolation.
    /// Call before each iteration since <see cref="CirrusServiceProvider.Dispose"/>
    /// clears all registrations.
    /// </summary>
    protected void RegisterBenchmarkStubs()
    {
        CirrusServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<IHttpTerminateHandler, StubHttpTerminateHandler>();
            services.AddSingleton<ITelemetryService, StubTelemetryService>();
            services.AddSingleton<IAuthContextService, StubAuthContextService>();
            services.AddSingleton<IAuthService, StubAuthService>();
        });
    }

    /// <summary>
    /// Cleanup: dispose DI container (also clears registrations).
    /// </summary>
    protected void CleanupCommon()
    {
        CirrusServiceProvider.Dispose();
    }
}
