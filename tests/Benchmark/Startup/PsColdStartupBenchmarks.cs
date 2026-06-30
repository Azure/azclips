// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Benchmark.Startup;

/// <summary>
/// PowerShell cold startup: OS page cache evicted before each launch. Only registered by
/// Program.cs when eviction is available.
/// </summary>
[SimpleJob(RunStrategy.ColdStart, iterationCount: 100)]
public class PsColdStartupBenchmarks : StartupBenchmarkBase
{
    [GlobalSetup]
    public void Setup() => InitializeCommon();

    [IterationSetup]
    public void Evict() => StartupEviction.Evict();

    [Benchmark(Description = "config")]
    public void Config() => RunToExit(PsStartInfo(StagedModulePath("Config"), "Get-AzConfigList"));

    [Benchmark(Description = "login")]
    public void Login() => RunToExit(PsStartInfo(StagedModulePath("Login"), "Get-AzAccount"));
}
