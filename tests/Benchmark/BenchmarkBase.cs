// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;
using Core.Abstractions.Runtime;
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
    /// Initialize stub HTTP and find repo root.
    /// </summary>
    protected void InitializeCommon()
    {
        CirrusServiceProvider.OverrideRegistration = services =>
        {
            services.AddSingleton<ICommandHttpMessageHandler, StubCommandMessageHandler>();
            services.AddSingleton<ITelemetryService, StubTelemetryService>();
            services.AddSingleton<IAuthContextService, StubAuthContextService>();
            services.AddSingleton<IAuthService, StubAuthService>();
        };

        RepoRoot = RepoLocator.GetRepoRoot();
    }

    /// <summary>
    /// Cleanup stub HTTP override and dispose DI container.
    /// </summary>
    protected void CleanupCommon()
    {
        CirrusServiceProvider.Dispose();
        CirrusServiceProvider.OverrideRegistration = null;
    }


}
