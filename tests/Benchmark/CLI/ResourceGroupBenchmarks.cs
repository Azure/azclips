// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace Benchmark.CLI;

public class ResourceGroupBenchmarks : CLIBenchmarkBase
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
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        CleanupCommon();
    }

    [Benchmark(Description = "CLI: List RG")]
    public int ListResourceGroups()
    {
        return Core.Cli.Program.Main(["group", "list", "--subscription-id", SubscriptionId]).GetAwaiter().GetResult();
    }

    [Benchmark(Description = "CLI: Create RG")]
    public int CreateResourceGroup()
    {
        var rgName = $"bench-rg-{Interlocked.Increment(ref RgCounter)}";
        return Core.Cli.Program.Main(["group", "create", "--name", rgName, "--location", "eastus", "--subscription-id", SubscriptionId]).GetAwaiter().GetResult();
    }

    [Benchmark(Description = "CLI: Get RG")]
    public int GetResourceGroup()
    {
        return Core.Cli.Program.Main(["group", "show", "--name", "bench-rg-get", "--subscription-id", SubscriptionId]).GetAwaiter().GetResult();
    }
}
