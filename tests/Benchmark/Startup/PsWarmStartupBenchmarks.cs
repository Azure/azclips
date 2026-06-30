// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Benchmark.Startup;

/// <summary>
/// PowerShell warm startup: fresh pwsh process, Import-Module
/// triggers service provider initialization + first cmdlet, OS page
/// cache hot. Config imports Login transitively (RequiredModules). Stops at the resolve-only seam.
/// </summary>
[SimpleJob(RunStrategy.Monitoring, warmupCount: 10, iterationCount: 100)]
public class PsWarmStartupBenchmarks : StartupBenchmarkBase
{
    [GlobalSetup]
    public void Setup() => InitializeCommon();

    [Benchmark(Description = "config")]
    public void Config() => RunToExit(PsStartInfo(StagedModulePath("Config"), "Get-AzConfigList"));

    [Benchmark(Description = "login")]
    public void Login() => RunToExit(PsStartInfo(StagedModulePath("Login"), "Get-AzAccount"));
}
