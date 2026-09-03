// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Benchmark.Startup;

/// <summary>
/// CLI warm startup: fresh az process per launch, OS page cache hot (BDN warmup primes it).
/// Stops at the resolve-only seam. Measures process create + runtime init + DI + extension load
/// + command resolve for each built-in.
/// </summary>
[SimpleJob(RunStrategy.Monitoring, warmupCount: 10, iterationCount: 100)]
public class CliWarmStartupBenchmarks : StartupBenchmarkBase
{
    [GlobalSetup]
    public void Setup() => InitializeCommon();

    [Benchmark(Description = "config")]
    public void Config() => RunToExit(CliStartInfo("config", "list"));

    [Benchmark(Description = "login")]
    public void Login() => RunToExit(CliStartInfo("login", "--subscription-id", "00000000-0000-0000-0000-000000000000"));

    [Benchmark(Description = "extension")]
    public void Extension() => RunToExit(CliStartInfo("extension", "list"));
}
