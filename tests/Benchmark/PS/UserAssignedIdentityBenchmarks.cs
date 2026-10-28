// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using BenchmarkDotNet.Attributes;

namespace Benchmark.PS;

[BenchmarkExtension("ManagedServiceIdentity")]
public class UserAssignedIdentityBenchmarks : PSBenchmarkBase
{
    private const string ResourceGroup = "bench-rg";
    private int _identityCounter = 0;

    [Benchmark(Description = "PS: List User Assigned Identity")]
    public object? ListUserAssignedIdentities()
    {
        return RunPowerShellCommand("Get-AzUserAssignedIdentity",
            ("SubscriptionId", SubscriptionId),
            ("ResourceGroupName", ResourceGroup));
    }

    [Benchmark(Description = "PS: Create User Assigned Identity")]
    public object? CreateUserAssignedIdentity()
    {
        var n = Interlocked.Increment(ref _identityCounter);
        return RunPowerShellCommand("New-AzUserAssignedIdentity",
            ("SubscriptionId", SubscriptionId),
            ("ResourceGroupName", ResourceGroup),
            ("Name", $"bench-identity-{n}"),
            ("Location", "eastus"),
            ("Tag", new Hashtable { ["env"] = "benchmark" }),
            ("IsolationScope", "Regional"),
            ("ResourceRestriction", new[] { "Microsoft.Compute" }));
    }

    [Benchmark(Description = "PS: Get User Assigned Identity")]
    public object? GetUserAssignedIdentity()
    {
        return RunPowerShellCommand("Get-AzUserAssignedIdentity",
            ("SubscriptionId", SubscriptionId),
            ("ResourceGroupName", ResourceGroup),
            ("Name", "bench-identity"));
    }

    [Benchmark(Description = "PS: Update User Assigned Identity")]
    public object? UpdateUserAssignedIdentity()
    {
        return RunPowerShellCommand("Update-AzUserAssignedIdentity",
            ("SubscriptionId", SubscriptionId),
            ("ResourceGroupName", ResourceGroup),
            ("Name", "bench-identity"),
            ("Tag", new Hashtable { ["env"] = "benchmark-updated" }),
            ("IsolationScope", "None"));
    }

    [Benchmark(Description = "PS: Delete User Assigned Identity")]
    public object? DeleteUserAssignedIdentity()
    {
        return RunPowerShellCommand("Remove-AzUserAssignedIdentity",
            ("SubscriptionId", SubscriptionId),
            ("ResourceGroupName", ResourceGroup),
            ("Name", "bench-identity"),
            ("Confirm", false));
    }
}
