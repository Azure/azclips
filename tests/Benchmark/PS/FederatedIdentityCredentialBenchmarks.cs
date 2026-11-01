// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace Benchmark.PS;

[BenchmarkExtension("ManagedServiceIdentity")]
public class FederatedIdentityCredentialBenchmarks : PSBenchmarkBase
{
    private const string ResourceGroup = "bench-rg";
    private const string IdentityName = "bench-identity";
    private const string Issuer = "https://token.actions.githubusercontent.com";
    private const string Subject = "repo:contoso/example:ref:refs/heads/main";
    private int _credentialCounter = 0;

    [Benchmark(Description = "PS: List Federated Identity Credential")]
    public object? ListFederatedIdentityCredentials()
    {
        return RunPowerShellCommand("Get-AzFederatedIdentityCredential",
            ("SubscriptionId", SubscriptionId),
            ("ResourceGroupName", ResourceGroup),
            ("IdentityName", IdentityName));
    }

    [Benchmark(Description = "PS: Create Federated Identity Credential")]
    public object? CreateFederatedIdentityCredential()
    {
        var n = Interlocked.Increment(ref _credentialCounter);
        return RunPowerShellCommand("New-AzFederatedIdentityCredential",
            ("SubscriptionId", SubscriptionId),
            ("ResourceGroupName", ResourceGroup),
            ("IdentityName", IdentityName),
            ("Name", $"bench-credential-{n}"),
            ("Issuer", Issuer),
            ("Subject", Subject));
    }

    [Benchmark(Description = "PS: Get Federated Identity Credential")]
    public object? GetFederatedIdentityCredential()
    {
        return RunPowerShellCommand("Get-AzFederatedIdentityCredential",
            ("SubscriptionId", SubscriptionId),
            ("ResourceGroupName", ResourceGroup),
            ("IdentityName", IdentityName),
            ("Name", "bench-credential"));
    }

    [Benchmark(Description = "PS: Update Federated Identity Credential")]
    public object? UpdateFederatedIdentityCredential()
    {
        return RunPowerShellCommand("Update-AzFederatedIdentityCredential",
            ("SubscriptionId", SubscriptionId),
            ("ResourceGroupName", ResourceGroup),
            ("IdentityName", IdentityName),
            ("Name", "bench-credential"),
            ("Audience", new[] { "api://BenchmarkAudience" }));
    }

    [Benchmark(Description = "PS: Delete Federated Identity Credential")]
    public object? DeleteFederatedIdentityCredential()
    {
        return RunPowerShellCommand("Remove-AzFederatedIdentityCredential",
            ("SubscriptionId", SubscriptionId),
            ("ResourceGroupName", ResourceGroup),
            ("IdentityName", IdentityName),
            ("Name", "bench-credential"),
            ("Confirm", false));
    }
}
