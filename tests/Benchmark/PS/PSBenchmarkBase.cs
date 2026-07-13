// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using BenchmarkDotNet.Attributes;
using Core.DependencyInjection;
using Perfolizer.Mathematics.OutlierDetection;

namespace Benchmark.PS;

[MemoryDiagnoser]
[InvocationCount(invocationCount: 1, unrollFactor: 1)]
[WarmupCount(5000)]
[IterationCount(5000)]
[Outliers(OutlierMode.RemoveAll)]
public abstract class PSBenchmarkBase : BenchmarkBase
{
    /// <summary>
    /// The PowerShell module this benchmark exercises. Only this module and its
    /// <c>RequiredModules</c> dependency closure are staged for the run.
    /// </summary>
    protected abstract string ModuleName { get; }

    private string _psModuleManifestPath = null!;
    private Runspace _runspace = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        InitializeCommon();

        _psModuleManifestPath = PSManifestStager.StageModule(RepoRoot, ModuleName);

        Console.WriteLine($"[Setup] PS benchmarks ready for '{ModuleName}' (stub HTTP)");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        RegisterBenchmarkStubs();

        // Create and open a fresh runspace here in setup so the cost of standing up the
        // PowerShell environment is NOT included in the measured region.
        _runspace = RunspaceFactory.CreateRunspace();
        _runspace.Open();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        CleanupCommon();

        _runspace?.Close();
        _runspace?.Dispose();
        _runspace = null!;
    }

    protected object? RunPowerShellCommand(
        string command,
        params (string Name, object Value)[] parameters)
    {
        // The runspace is created in [IterationSetup] (unmeasured). The measured region
        // below covers only the module import and the cmdlet invocation.

        // Import module — triggers AzclipsServiceProvider.Initialize(PowerShell) in .psm1
        using (var import = PowerShell.Create())
        {
            import.Runspace = _runspace;
            import.AddCommand("Import-Module").AddParameter("Name", _psModuleManifestPath).AddParameter("Force", true);
            import.Invoke();
        }

        // Run the cmdlet
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
