// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Core.Abstractions;
using Core.Cli;
using Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Perfolizer.Mathematics.OutlierDetection;

namespace Benchmark.CLI;

[MemoryDiagnoser]
[InvocationCount(invocationCount: 1, unrollFactor: 1)]
[MaxRelativeError(0.01)]
[MaxIterationCount(1000)]
[Outliers(OutlierMode.RemoveAll)]
public abstract class CLIBenchmarkBase : BenchmarkBase
{
    [GlobalSetup]
    public void GlobalSetup()
    {
        InitializeCommon();
        Console.WriteLine("[Setup] CLI benchmarks ready (stub HTTP)");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        RegisterBenchmarkStubs();
        RegisterExtensionLoaders();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        CleanupCommon();
    }
    private void RegisterExtensionLoaders()
    {
        var installationDir = Path.Combine(RepoRoot, "artifacts", "Release");

        CirrusServiceProvider.OverrideRegistration(services =>
        {
            services.RemoveAll<IExtensionLoader>();
            services.AddSingleton<IExtensionLoader>(sp => new BuiltinExtensionLoader(
                sp.GetRequiredService<ILoggingService>(),
                new BenchmarkAssemblyConfigurationService(installationDir)));
            services.AddSingleton<IExtensionLoader>(sp => new MultiVersionExtensionLoader(
                sp.GetRequiredService<ILoggingService>(),
                new BenchmarkAssemblyConfigurationService(installationDir)));
        });
    }
}
