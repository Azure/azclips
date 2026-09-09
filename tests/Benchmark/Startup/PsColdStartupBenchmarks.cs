// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

    [IterationSetup]
    public void Evict() => StartupEviction.Evict();

    [Benchmark(Description = "config")]
    public void Config() => RunToExit(PsStartInfo(_configManifest, "Get-AzConfigList"));

    [Benchmark(Description = "login")]
    public void Login() => RunToExit(PsStartInfo(_accountsManifest, "Connect-AzAccount", "-SubscriptionId", "00000000-0000-0000-0000-000000000000"));
}
