// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace Benchmark.CLI;

public class ResourceGroupBenchmarks : CLIBenchmarkBase
{
    protected override IReadOnlyList<string> RequiredExtensions => new[] { "Resources" };

    [Benchmark(Description = "CLI: List RG")]
    public int ListResourceGroups()
    {
        var exitCode = Core.Cli.Program.Main(["group", "list", "--subscription-id", SubscriptionId]).GetAwaiter().GetResult();
        if (exitCode != 0) throw new InvalidOperationException($"CLI exited with code {exitCode}");
        return exitCode;
    }

    [Benchmark(Description = "CLI: Create RG")]
    public int CreateResourceGroup()
    {
        var rgName = $"bench-rg-{Interlocked.Increment(ref RgCounter)}";
        var exitCode = Core.Cli.Program.Main(["group", "create", "--name", rgName, "--location", "eastus", "--subscription-id", SubscriptionId]).GetAwaiter().GetResult();
        if (exitCode != 0) throw new InvalidOperationException($"CLI exited with code {exitCode}");
        return exitCode;
    }

    [Benchmark(Description = "CLI: Get RG")]
    public int GetResourceGroup()
    {
        var exitCode = Core.Cli.Program.Main(["group", "show", "--name", "bench-rg-get", "--subscription-id", SubscriptionId]).GetAwaiter().GetResult();
        if (exitCode != 0) throw new InvalidOperationException($"CLI exited with code {exitCode}");
        return exitCode;
    }
}
