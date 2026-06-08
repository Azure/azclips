// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using BenchmarkDotNet.Attributes;
using Core.DependencyInjection;

namespace Benchmark.PS;

public class ResourceGroupBenchmarks : PSBenchmarkBase
{
    [GlobalSetup]
    public void GlobalSetup()
    {
        InitializeCommon();
        Console.WriteLine("[Setup] PS benchmarks ready (stub HTTP)");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        RegisterBenchmarkStubs();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        CleanupCommon();
    }

    private object? RunPowerShellCommand(string command, params (string Name, object Value)[] parameters)
    {
        // Import from the staged artifact manifest, not src/PS/. The CI workflow
        // (and local devs) run build/Set-ModuleConfiguration.ps1 to write a
        // configuration-specific copy to artifacts/<Config>/PS/Resources/Resources.psd1.
        //
        // RepoRoot is resolved by RepoLocator (scans upward for cirrus.slnx), so it
        // works even from BenchmarkDotNet's nested harness directory
        // (artifacts/<Config>/Benchmark/<guid>/bin/<Config>/net10.0). AppContext.BaseDirectory
        // cannot be used here because of that nesting.
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        var psModulePath = Path.Combine(
            RepoRoot, "artifacts", configuration, "PS", "Resources", "Resources.psd1");

        // Create and open a fresh runspace per invocation (cold start)
        var runspace = RunspaceFactory.CreateRunspace();
        runspace.Open();

        try
        {
            // Import module — triggers CirrusServiceProvider.Initialize(PowerShell) in .psm1
            using (var import = PowerShell.Create())
            {
                import.Runspace = runspace;
                import.AddCommand("Import-Module").AddParameter("Name", psModulePath).AddParameter("Force", true);
                import.Invoke();
            }

            // Run the cmdlet
            using var ps = PowerShell.Create();
            ps.Runspace = runspace;
            ps.AddCommand(command);
            foreach (var (name, value) in parameters)
            {
                ps.AddParameter(name, value);
            }
            var results = ps.Invoke();
            return results.Count > 0 ? results[0] : null;
        }
        finally
        {
            CirrusServiceProvider.Dispose();
            runspace.Close();
            runspace.Dispose();
        }
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
        var rgName = $"bench-rg-{Interlocked.Increment(ref RgCounter)}";
        return RunPowerShellCommand("New-ResourceGroup",
            ("SubscriptionId", SubscriptionId),
            ("Name", rgName),
            ("Location", "eastus"));
    }

    [Benchmark(Description = "PS: Get RG")]
    public object? GetResourceGroup()
    {
        return RunPowerShellCommand("Get-ResourceGroup",
            ("SubscriptionId", SubscriptionId),
            ("Name", "bench-rg-get"));
    }
}
