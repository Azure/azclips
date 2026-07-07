// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace Benchmark.PS;

/// <summary>
/// PS benchmarks — runs cmdlets with mocked HTTP. Does NOT hit Azure.
/// Cold-start benchmarks: fresh runspace + module import + DI init + engine process + dispose per call.
/// </summary>
public class ResourceGroupBenchmarks : PSBenchmarkBase
{
    protected override string ModuleName => "Resources";

    [Benchmark(Description = "PS: List RG")]
    public object? ListResourceGroups()
    {
        return RunPowerShellCommand("Get-ResourceGroup");
    }

    [Benchmark(Description = "PS: Create RG")]
    public object? CreateResourceGroup()
    {
        var rgName = $"bench-rg-{Interlocked.Increment(ref RgCounter)}";
        return RunPowerShellCommand("New-ResourceGroup",
            ("SubscriptionId", SubscriptionId),
            ("Name", rgName),
            ("Location", "eastus"));
    }

    [Benchmark(Description = "PS: Get RG")]
    public object? GetResourceGroup()
    {
        return RunPowerShellCommand("Get-ResourceGroup",
            ("Name", "bench-rg-get"));
    }
}
