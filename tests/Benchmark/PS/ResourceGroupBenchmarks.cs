// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Core.Abstractions;
using Core.DependencyInjection;

namespace Benchmark.PS;

/// <summary>
/// PS benchmarks — runs cmdlets in a persistent runspace with mocked HTTP.
/// Measures: cmdlet execution, DI resolution, command binding, serialization,
/// deserialization, output routing. Does NOT hit Azure.
/// </summary>
[MemoryDiagnoser]
[WarmupCount(1)]
[IterationCount(5)]
[SimpleJob(RunStrategy.Monitoring, iterationCount: 5)]
public class ResourceGroupBenchmarks : BenchmarkBase
{
    private Runspace _runspace = null!;
    private const string DummySubscriptionId = "00000000-0000-0000-0000-000000000000";

    [GlobalSetup]
    public void Setup()
    {
        InitializeCommon(ClientType.PowerShell);

        var psModulePath = Path.Combine(RepoRoot, "src", "PS", "Resources", "Resources.psd1");

        // Create and open persistent runspace
        _runspace = RunspaceFactory.CreateRunspace();
        _runspace.Open();

        // Import module — Initialize() in .psm1 is a no-op since container is already built
        using var ps = PowerShell.Create();
        ps.Runspace = _runspace;
        ps.AddCommand("Import-Module").AddParameter("Name", psModulePath).AddParameter("Force", true);
        ps.Invoke();

        Console.WriteLine("[Setup] PS benchmarks ready (stub HTTP)");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _runspace?.Close();
        _runspace?.Dispose();
        CleanupCommon();
    }

    [Benchmark(Description = "PS: List RG")]
    public object? ListResourceGroups()
    {
        return RunPowerShellCommand("Get-ResourceGroup",
            ("SubscriptionId", DummySubscriptionId));
    }

    [Benchmark(Description = "PS: Create RG")]
    public object? CreateResourceGroup()
    {
        var rgName = $"bench-rg-{Interlocked.Increment(ref RgCounter)}";
        return RunPowerShellCommand("New-ResourceGroup",
            ("SubscriptionId", DummySubscriptionId),
            ("Name", rgName),
            ("Location", "eastus"));
    }

    [Benchmark(Description = "PS: Get RG")]
    public object? GetResourceGroup()
    {
        return RunPowerShellCommand("Get-ResourceGroup",
            ("SubscriptionId", DummySubscriptionId),
            ("Name", "bench-rg-get"));
    }

    private object? RunPowerShellCommand(string command, params (string Name, object Value)[] parameters)
    {
        using var ps = PowerShell.Create();
        ps.Runspace = _runspace;
        ps.AddCommand(command);
        foreach (var (name, value) in parameters)
        {
            ps.AddParameter(name, value);
        }
        var results = ps.Invoke();
        return results.Count > 0 ? results[0] : null;
    }
}
