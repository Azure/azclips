// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace Benchmark.CLI;

[BenchmarkExtension("ManagedServiceIdentity")]
public class UserAssignedIdentityBenchmarks : CLIBenchmarkBase
{
    private const string ResourceGroup = "bench-rg";
    private int _identityCounter = 0;

    [Benchmark(Description = "CLI: List User Assigned Identity")]
    public int ListUserAssignedIdentities() =>
        RunCli("identity", "list", "--subscription", SubscriptionId, "--resource-group", ResourceGroup);

    [Benchmark(Description = "CLI: Create User Assigned Identity")]
    public int CreateUserAssignedIdentity()
    {
        var n = Interlocked.Increment(ref _identityCounter);
        return RunCli(
            "identity", "create",
            "--subscription", SubscriptionId,
            "--resource-group", ResourceGroup,
            "--name", $"bench-identity-{n}",
            "--location", "eastus",
            "--tags", "env=benchmark",
            "--isolation-scope", "Regional",
            "--resource-restriction", """{"providers":["Microsoft.Compute"]}""");
    }

    [Benchmark(Description = "CLI: Get User Assigned Identity")]
    public int GetUserAssignedIdentity() =>
        RunCli("identity", "show", "--subscription", SubscriptionId, "--resource-group", ResourceGroup, "--name", "bench-identity");

    [Benchmark(Description = "CLI: Update User Assigned Identity")]
    public int UpdateUserAssignedIdentity() =>
        RunCli(
            "identity", "update",
            "--subscription", SubscriptionId,
            "--resource-group", ResourceGroup,
            "--name", "bench-identity",
            "--isolation-scope", "None",
            "--tags", "env=benchmark-updated");
}
