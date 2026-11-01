// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace Benchmark.CLI;

[BenchmarkExtension("ManagedServiceIdentity")]
public class FederatedIdentityCredentialBenchmarks : CLIBenchmarkBase
{
    private const string ResourceGroup = "bench-rg";
    private const string IdentityName = "bench-identity";
    private const string Issuer = "https://token.actions.githubusercontent.com";
    private const string Subject = "repo:contoso/example:ref:refs/heads/main";
    private int _credentialCounter = 0;

    [Benchmark(Description = "CLI: List Federated Identity Credential")]
    public int ListFederatedIdentityCredentials() =>
        RunCli(
            "identity", "federated-credential", "list",
            "--subscription", SubscriptionId,
            "--resource-group", ResourceGroup,
            "--identity-name", IdentityName);

    [Benchmark(Description = "CLI: Create Federated Identity Credential")]
    public int CreateFederatedIdentityCredential()
    {
        var n = Interlocked.Increment(ref _credentialCounter);
        return RunCli(
            "identity", "federated-credential", "create",
            "--subscription", SubscriptionId,
            "--resource-group", ResourceGroup,
            "--identity-name", IdentityName,
            "--name", $"bench-credential-{n}",
            "--issuer", Issuer,
            "--subject", Subject);
    }

    [Benchmark(Description = "CLI: Get Federated Identity Credential")]
    public int GetFederatedIdentityCredential() =>
        RunCli(
            "identity", "federated-credential", "show",
            "--subscription", SubscriptionId,
            "--resource-group", ResourceGroup,
            "--identity-name", IdentityName,
            "--name", "bench-credential");

    [Benchmark(Description = "CLI: Update Federated Identity Credential")]
    public int UpdateFederatedIdentityCredential() =>
        RunCli(
            "identity", "federated-credential", "update",
            "--subscription", SubscriptionId,
            "--resource-group", ResourceGroup,
            "--identity-name", IdentityName,
            "--name", "bench-credential",
            "--audiences", "api://BenchmarkAudience");

    [Benchmark(Description = "CLI: Delete Federated Identity Credential")]
    public int DeleteFederatedIdentityCredential() =>
        RunCli(
            "identity", "federated-credential", "delete",
            "--subscription", SubscriptionId,
            "--resource-group", ResourceGroup,
            "--identity-name", IdentityName,
            "--name", "bench-credential");
}
