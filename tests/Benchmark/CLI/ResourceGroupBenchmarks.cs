// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Core.Abstractions;

namespace Benchmark.CLI;

/// <summary>
/// CLI benchmarks — runs Program.Run() in-process with mocked HTTP.
/// Measures: DI init, parsing, extension loading, command binding, auth token injection,
/// serialization, deserialization, output routing. Does NOT hit Azure.
/// </summary>
[MemoryDiagnoser]
[WarmupCount(1)]
[IterationCount(5)]
[InProcess]
public class ResourceGroupBenchmarks : BenchmarkBase
{
    [GlobalSetup]
    public void Setup()
    {
        InitializeCommon();
        Console.WriteLine("[Setup] CLI benchmarks ready (stub HTTP)");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        CleanupCommon();
    }

    [Benchmark(Description = "CLI: group list")]
    public int GroupList()
    {
        return Core.Cli.Program.Main(new[] { "group", "list" }).GetAwaiter().GetResult();
    }

    [Benchmark(Description = "CLI: group create")]
    public int GroupCreate()
    {
        var rgName = $"bench-rg-{Interlocked.Increment(ref RgCounter)}";
        return Core.Cli.Program.Main(new[] { "group", "create", "--name", rgName, "--location", "eastus", "--subscription-id", SubscriptionId}).GetAwaiter().GetResult();
    }

    [Benchmark(Description = "CLI: group show")]
    public int GroupShow()
    {
        return Core.Cli.Program.Main(new[] { "group", "show", "--Name", "bench-rg-show" }).GetAwaiter().GetResult();
    }
}
