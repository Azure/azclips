// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace Benchmark.CLI;

[BenchmarkExtension("Resources")]
public class ResourceGroupBenchmarks : CLIBenchmarkBase
{
    private int _rgCounter = 0;

    [Benchmark(Description = "CLI: List RG")]
    public int ListResourceGroups() =>
        RunCli("group", "list", "--subscription-id", SubscriptionId);

    [Benchmark(Description = "CLI: Create RG")]
    public int CreateResourceGroup()
    {
        var rgName = $"bench-rg-{Interlocked.Increment(ref _rgCounter)}";
        return RunCli("group", "create", "--name", rgName, "--location", "eastus", "--subscription-id", SubscriptionId);
    }

    [Benchmark(Description = "CLI: Get RG")]
    public int GetResourceGroup() =>
        RunCli("group", "show", "--name", "bench-rg-get", "--subscription-id", SubscriptionId);
}
