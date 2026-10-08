// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace Benchmark.PS;

/// <summary>
/// PS benchmarks for the Compute extension — Virtual Machine List / Create / Get,
/// run against the resource-aware stub (no Azure calls). `Get-VM` without -Name lists;
/// with -Name gets. `New-VM` runs the orchestrated create (VNet -> NSG -> NIC -> VM).
/// Cold start: fresh runspace + module import + DI init per call.
/// </summary>
[BenchmarkExtension("Compute")]
public class VirtualMachineBenchmarks : PSBenchmarkBase
{
    private const string ResourceGroup = "bench-rg";
    private int _vmCounter = 0;

    [Benchmark(Description = "PS: List VM")]
    public object? ListVirtualMachines()
    {
        return RunPowerShellCommand("Get-AzVM",
            ("SubscriptionId", SubscriptionId),
            ("ResourceGroupName", ResourceGroup));
    }

    [Benchmark(Description = "PS: Create VM")]
    public object? CreateVirtualMachine()
    {
        var n = Interlocked.Increment(ref _vmCounter);
        return RunPowerShellCommand("New-AzVM",
            ("SubscriptionId", SubscriptionId),
            ("Name", $"bench-vm-{n}"),
            ("Location", "eastus"),
            ("ResourceGroupName", ResourceGroup),
            ("VmSize", "Standard_D2s_v5"),
            ("AdminUsername", "benchadmin"),
            ("AdminPassword", "Benchmark!Pass123"),
            ("NetworkInterfaceName", $"bench-nic-{n}"),
            ("VirtualNetworkName", $"bench-vnet-{n}"));
    }

    [Benchmark(Description = "PS: Get VM")]
    public object? GetVirtualMachine()
    {
        return RunPowerShellCommand("Get-AzVM",
            ("SubscriptionId", SubscriptionId),
            ("ResourceGroupName", ResourceGroup),
            ("Name", "bench-vm-get"));
    }
}
