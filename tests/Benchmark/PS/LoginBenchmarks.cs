// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace Benchmark.PS;

/// <summary>
/// Login benchmarks through the PowerShell surface. Authentication and profile persistence use the
/// standard benchmark stubs, so these measure cmdlet binding and the shared Login processor without
/// contacting Azure or IMDS.
/// </summary>
[BenchmarkExtension("Login")]
public class LoginBenchmarks : PSBenchmarkBase
{
    private const string UserAssignedClientId = "e5f6a7b8-4567-4def-b234-56789abcdef0";

    protected override string ModuleName => "Login";

    [Benchmark(Description = "PS: Login - MI system-assigned")]
    public object? ManagedIdentitySystemAssigned()
    {
        return RunPowerShellCommand(
            "Connect-AzAccount",
            ("Identity", true),
            ("SubscriptionId", SubscriptionId));
    }

    [Benchmark(Description = "PS: Login - MI user-assigned client ID")]
    public object? ManagedIdentityUserAssignedClientId()
    {
        return RunPowerShellCommand(
            "Connect-AzAccount",
            ("Identity", true),
            ("SubscriptionId", SubscriptionId),
            ("ClientId", UserAssignedClientId));
    }
}
