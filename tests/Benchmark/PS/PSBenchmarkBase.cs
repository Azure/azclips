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
[IterationCount(10000)]
[Outliers(OutlierMode.RemoveAll)]
public abstract class PSBenchmarkBase : BenchmarkBase
{
    /// <summary>
    /// The PowerShell module this benchmark exercises. Only this module and its
    /// <c>RequiredModules</c> dependency closure are staged for the run. Defaults to the
    /// extension named by the class's <c>[BenchmarkExtension]</c> attribute; override only if
    /// the module name differs from the extension name.
    /// </summary>
    protected virtual string ModuleName => ExtensionName;

    private string _psModuleManifestPath = null!;

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
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        CleanupCommon();
    }

    protected object? RunPowerShellCommand(
        string command,
        params (string Name, object Value)[] parameters)
    {
        // Create and open a fresh runspace per invocation (cold start)
        var runspace = RunspaceFactory.CreateRunspace();
        runspace.Open();

        try
        {
            // Import module — triggers AzclipsServiceProvider.Initialize(PowerShell) in .psm1
            using (var import = PowerShell.Create())
            {
                import.Runspace = runspace;
                import.AddCommand("Import-Module").AddParameter("Name", _psModuleManifestPath).AddParameter("Force", true);
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
            AzclipsServiceProvider.Dispose();
            runspace.Close();
            runspace.Dispose();
        }
    }
}
