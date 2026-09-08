// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Benchmark.Startup;

/// <summary>
/// PowerShell warm startup: fresh pwsh process, Import-Module
/// triggers service provider initialization + first cmdlet, OS page
/// cache hot. Config imports Accounts transitively (RequiredModules). Stops at the resolve-only seam.
/// </summary>
[SimpleJob(RunStrategy.Monitoring, warmupCount: 10, iterationCount: 100)]
public class PsWarmStartupBenchmarks : StartupBenchmarkBase
{
    private string _configManifest = null!;
    private string _accountsManifest = null!;

    [GlobalSetup]
    public void Setup()
    {
        InitializeCommon();
        // Stage once, outside the measured region. Staging Config pulls in Accounts via RequiredModules.
        _configManifest = StageModule("Config");
        _accountsManifest = StageModule("Accounts");
    }

    [Benchmark(Description = "config")]
    public void Config() => RunToExit(PsStartInfo(_configManifest, "Get-AzConfigList"));

    [Benchmark(Description = "login")]
    public void Login() => RunToExit(PsStartInfo(_accountsManifest, "Connect-AzAccount", "-SubscriptionId", "00000000-0000-0000-0000-000000000000"));
}
