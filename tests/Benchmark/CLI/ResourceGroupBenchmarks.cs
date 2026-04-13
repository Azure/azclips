// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Benchmark.CLI;

/// <summary>
/// E2E Live benchmarks for Resource Group operations using CLI (azv2.exe).
/// Tests full CLI command execution time including startup, auth, parsing.
/// Requires: 'azv2 login' authentication and 'dotnet build' before running.
/// </summary>
[MemoryDiagnoser]
[WarmupCount(1)]
[IterationCount(5)]
[SimpleJob(RunStrategy.Monitoring, iterationCount: 5)]
public class ResourceGroupBenchmarks : BenchmarkBase
{
    private string _cliPath = null!;
    private string _showRgName = null!;

    [GlobalSetup]
    public void Setup()
    {
        InitializeCommon(Core.Abstractions.ClientType.CLI);

        // CLI executable path
        _cliPath = Path.Combine(RepoRoot, "artifacts", "Debug", "Core.Cli", "azv2.exe");
        if (!File.Exists(_cliPath))
        {
            throw new FileNotFoundException($"CLI executable not found at: {_cliPath}. Run 'dotnet build' first.");
        }
        Console.WriteLine($"[Setup] CLI path: {_cliPath}");

        // Pre-create a resource group for Show benchmark
        _showRgName = $"{TestRgPrefix}{InstanceId}-cli-show";
        RunCliCommand("group", "create", "--subscription-id", SubscriptionId, "--name", _showRgName, "--location", TestLocation);
        CreatedResourceGroups.Add(_showRgName);

        Console.WriteLine($"[Setup] CLI benchmarks ready");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        CleanupResourceGroups();
    }

    [Benchmark(Description = "CLI: group list")]
    public int GroupList()
    {
        return RunCliCommand("group", "list", "--subscription-id", SubscriptionId);
    }

    [Benchmark(Description = "CLI: group create")]
    public int GroupCreate()
    {
        var rgName = GenerateRgName("cli-");
        return RunCliCommand("group", "create", "--subscription-id", SubscriptionId, "--name", rgName, "--location", TestLocation);
    }

    [Benchmark(Description = "CLI: group show")]
    public int GroupShow()
    {
        return RunCliCommand("group", "show", "--subscription-id", SubscriptionId, "--name", _showRgName);
    }

    private int RunCliCommand(params string[] args)
    {
        return RunProcess(_cliPath, string.Join(" ", args));
    }
}
