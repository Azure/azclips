// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace Benchmark.CLI;

/// <summary>
/// CLI benchmarks for the Compute extension — Virtual Machine List / Create / Get,
/// run against the resource-aware stub (no Azure calls). Create exercises the
/// orchestrated `vm create` path (VNet -> NSG -> NIC -> VM). Cold start per call.
/// </summary>
[BenchmarkExtension("Compute")]
public class VirtualMachineBenchmarks : CLIBenchmarkBase
{
    private const string ResourceGroup = "bench-rg";
    private int VmCounter = 0;

    [Benchmark(Description = "CLI: List VM")]
    public int ListVirtualMachines() =>
        RunCli("vm", "list", "--subscription-id", SubscriptionId, "--resource-group", ResourceGroup);

    [Benchmark(Description = "CLI: Create VM")]
    public int CreateVirtualMachine()
    {
        var n = Interlocked.Increment(ref VmCounter);
        return RunCli(
            "vm", "create",
            "--subscription-id", SubscriptionId,
            "--name", $"bench-vm-{n}",
            "--location", "eastus",
            "--resource-group", ResourceGroup,
            "--size", "Standard_D2s_v5",
            "--admin-username", "benchadmin",
            "--admin-password", "Benchmark!Pass123",
            "--virtual-network-name", $"bench-vnet-{n}");
    }

    [Benchmark(Description = "CLI: Get VM")]
    public int GetVirtualMachine() =>
        RunCli("vm", "show", "--subscription-id", SubscriptionId, "--name", "bench-vm-get", "--resource-group", ResourceGroup);
}
