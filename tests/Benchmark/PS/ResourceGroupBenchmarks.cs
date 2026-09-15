// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace Benchmark.PS;

/// <summary>
/// PS benchmarks — runs cmdlets with mocked HTTP. Does NOT hit Azure.
/// A fresh runspace is created and the module imported per iteration in [IterationSetup]
/// (both excluded from the measurement); each measured run invokes only the cmdlet, so the
/// result is comparable to the CLI benchmark's warm dispatch.
/// </summary>
[BenchmarkExtension("Resources")]
public class ResourceGroupBenchmarks : PSBenchmarkBase
{
    private int _rgCounter = 0;
    
    [Benchmark(Description = "PS: List RG")]
    public object? ListResourceGroups()
    {
        return RunPowerShellCommand("Get-AzResourceGroup",
            ("SubscriptionId", SubscriptionId));
    }

    [Benchmark(Description = "PS: Create RG")]
    public object? CreateResourceGroup()
    {
        var rgName = $"bench-rg-{Interlocked.Increment(ref _rgCounter)}";
        return RunPowerShellCommand("New-AzResourceGroup",
            ("SubscriptionId", SubscriptionId),
            ("Name", rgName),
            ("Location", "eastus"));
    }

    [Benchmark(Description = "PS: Get RG")]
    public object? GetResourceGroup()
    {
        return RunPowerShellCommand("Get-AzResourceGroup",
            ("SubscriptionId", SubscriptionId),
            ("Name", "bench-rg-get"));
    }
}
