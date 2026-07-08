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
    protected override IReadOnlyList<string> RequiredExtensions => new[] { "Compute" };

    private const string ResourceGroup = "bench-rg";

    [Benchmark(Description = "CLI: List VM")]
    public int ListVirtualMachines()
    {
        var exitCode = Core.Cli.Program.Main(
            ["vm", "list", "--subscription-id", SubscriptionId, "--resource-group", ResourceGroup])
            .GetAwaiter().GetResult();
        if (exitCode != 0) throw new InvalidOperationException($"CLI exited with code {exitCode}");
        return exitCode;
    }

    [Benchmark(Description = "CLI: Create VM")]
    public int CreateVirtualMachine()
    {
        var n = Interlocked.Increment(ref VmCounter);
        var exitCode = Core.Cli.Program.Main(
        [
            "vm", "create",
            "--subscription-id", SubscriptionId,
            "--name", $"bench-vm-{n}",
            "--location", "eastus",
            "--resource-group", ResourceGroup,
            "--vm-size", "Standard_D2s_v5",
            "--admin-username", "benchadmin",
            "--admin-password", "Benchmark!Pass123",
            "--network-interface-name", $"bench-nic-{n}",
            "--virtual-network-name", $"bench-vnet-{n}",
            "--network-security-group-name", $"bench-nsg-{n}"
        ]).GetAwaiter().GetResult();
        if (exitCode != 0) throw new InvalidOperationException($"CLI exited with code {exitCode}");
        return exitCode;
    }

    [Benchmark(Description = "CLI: Get VM")]
    public int GetVirtualMachine()
    {
        var exitCode = Core.Cli.Program.Main(
            ["vm", "show", "--subscription-id", SubscriptionId, "--name", "bench-vm-get", "--resource-group", ResourceGroup])
            .GetAwaiter().GetResult();
        if (exitCode != 0) throw new InvalidOperationException($"CLI exited with code {exitCode}");
        return exitCode;
    }
}
