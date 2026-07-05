// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace Benchmark.CLI;

/// <summary>
/// Login benchmarks. These run entirely in-process against the standard benchmark
/// stubs (<see cref="BenchmarkBase.RegisterBenchmarkStubs"/> swaps in a stub <c>IAuthService</c>), so
/// they measure the CLI login pipeline — argument parsing, built-in extension dispatch, and profile
/// persistence — without spawning an <c>az</c> subprocess or standing up a fake IMDS endpoint.
/// A subscription id is always supplied so the login skips subscription discovery.
/// <para>
/// <c>login</c> is a built-in verb (<c>BuiltInExtensions.Map</c>), so it is resolved from the seeded
/// host extensions rather than registry-staged; the built-in loader is pointed at the real Release
/// build output where the Login extension assembly lives.
/// </para>
/// </summary>
[BenchmarkExtension("Login")]
public class LoginBenchmarks : CLIBenchmarkBase
{
    // Arbitrary well-formed GUID for the user-assigned managed identity selector.
    private const string UserAssignedClientId = "e5f6a7b8-4567-4def-b234-56789abcdef0";

    // Built-in verbs (login/logout/profile) can't be registry-staged; probe the real build output.
    protected override string BuiltinInstallationDir => Path.Combine(RepoRoot, "artifacts", "Release");

    [Benchmark(Description = "CLI: Login - MI system-assigned")]
    public int ManagedIdentitySystemAssigned()
    {
        var exitCode = Core.Cli.Program.Main(
            ["login", "--identity", "--subscription-id", SubscriptionId]).GetAwaiter().GetResult();
        if (exitCode != 0) throw new InvalidOperationException($"CLI exited with code {exitCode}");
        return exitCode;
    }

    [Benchmark(Description = "CLI: Login - MI user-assigned client ID")]
    public int ManagedIdentityUserAssignedClientId()
    {
        var exitCode = Core.Cli.Program.Main(
            ["login", "--identity", "--subscription-id", SubscriptionId, "--client-id", UserAssignedClientId]).GetAwaiter().GetResult();
        if (exitCode != 0) throw new InvalidOperationException($"CLI exited with code {exitCode}");
        return exitCode;
    }
}
