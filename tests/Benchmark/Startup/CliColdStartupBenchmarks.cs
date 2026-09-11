// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Benchmark.Startup;

/// <summary>
/// CLI cold startup: OS page cache evicted before each launch, so DLLs are re-read from disk
/// (the "first run after reboot" case). Only registered by Program.cs when eviction is available.
/// </summary>
[SimpleJob(RunStrategy.ColdStart, iterationCount: 100)]
public class CliColdStartupBenchmarks : StartupBenchmarkBase
{
    [GlobalSetup]
    public void Setup() => InitializeCommon();

    [IterationSetup]
    public void Evict() => StartupEviction.Evict();

    [Benchmark(Description = "config")]
    public void Config() => RunToExit(CliStartInfo("config", "list"));

    [Benchmark(Description = "login")]
    public void Login() => RunToExit(CliStartInfo("login", "--subscription-id", "00000000-0000-0000-0000-000000000000"));

    [Benchmark(Description = "extension")]
    public void Extension() => RunToExit(CliStartInfo("extension", "list"));
}
