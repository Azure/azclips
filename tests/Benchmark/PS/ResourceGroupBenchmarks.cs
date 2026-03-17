// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Benchmark.PS;

/// <summary>
/// E2E Live benchmarks for Resource Group operations using PowerShell.
/// Uses persistent runspace to avoid process startup and module loading overhead.
/// Requires: 'azv2 login' authentication before running.
/// </summary>
[MemoryDiagnoser]
[WarmupCount(1)]
[IterationCount(5)]
[SimpleJob(RunStrategy.Monitoring, iterationCount: 5)]
public class ResourceGroupBenchmarks : BenchmarkBase
{
    private Runspace _runspace = null!;
    private string _psModulePath = null!;
    private string _getRgName = null!;

    [GlobalSetup]
    public void Setup()
    {
        InitializeCommon(Core.Abstractions.ClientType.PowerShell);

        // PS Module path
        _psModulePath = Path.Combine(RepoRoot, "src", "PS", "Resources", "Resources.psd1");
        Console.WriteLine($"[Setup] PS Module path: {_psModulePath}");

        // Create and open a persistent runspace
        _runspace = RunspaceFactory.CreateRunspace();
        _runspace.Open();

        // Import module once in setup (this cost is excluded from benchmark)
        using var ps = PowerShell.Create();
        ps.Runspace = _runspace;
        ps.AddCommand("Import-Module").AddParameter("Name", _psModulePath).AddParameter("Force", true);
        ps.Invoke();
        Console.WriteLine($"[Setup] Module imported");

        // Pre-create a resource group for Get benchmark
        _getRgName = $"{TestRgPrefix}{InstanceId}-ps-get";
        RunPowerShellCommand("New-ResourceGroup", 
            ("SubscriptionId", SubscriptionId), 
            ("Name", _getRgName), 
            ("Location", TestLocation));
        CreatedResourceGroups.Add(_getRgName);

        Console.WriteLine($"[Setup] PS benchmarks ready");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _runspace?.Close();
        _runspace?.Dispose();
        CleanupResourceGroups();
    }

    [Benchmark(Description = "PS: List RG")]
    public object? ListResourceGroups()
    {
        return RunPowerShellCommand("Get-ResourceGroup", 
            ("SubscriptionId", SubscriptionId));
    }

    [Benchmark(Description = "PS: Create RG")]
    public object? CreateResourceGroup()
    {
        var rgName = GenerateRgName("ps-");
        return RunPowerShellCommand("New-ResourceGroup", 
            ("SubscriptionId", SubscriptionId), 
            ("Name", rgName), 
            ("Location", TestLocation));
    }

    [Benchmark(Description = "PS: Get RG")]
    public object? GetResourceGroup()
    {
        return RunPowerShellCommand("Get-ResourceGroup", 
            ("SubscriptionId", SubscriptionId), 
            ("Name", _getRgName));
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
