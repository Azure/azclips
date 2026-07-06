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

    [IterationCleanup]
    public void IterationCleanup()
    {
        CleanupCommon();
    }

    protected object? RunPowerShellCommand(
        string moduleName,
        string command,
        params (string Name, object Value)[] parameters)
    {
        // Always resolve to "Release" because BDN always run benchmarks in release mode.
        var psModulePath = Path.Combine(
            RepoRoot, "artifacts", "Release", "PS", moduleName, $"{moduleName}.psd1");

        // Create and open a fresh runspace per invocation (cold start)
        var runspace = RunspaceFactory.CreateRunspace();
        runspace.Open();

        try
        {
            // Import the Login module first — its ModuleInitializer.OnImport is the only
            // code that calls AzclipsServiceProvider.Initialize(PowerShell). Staged manifests
            // no longer carry RequiredModules (Set-ModuleConfiguration.ps1 strips them for
            // non-Login modules), so we must import Login explicitly to bootstrap DI.
            if (!string.Equals(moduleName, "Login", StringComparison.OrdinalIgnoreCase))
            {
                var loginModulePath = Path.Combine(
                    RepoRoot, "artifacts", "Release", "PS", "Login", "Login.psd1");
                using var loginImport = PowerShell.Create();
                loginImport.Runspace = runspace;
                loginImport.AddCommand("Import-Module").AddParameter("Name", loginModulePath).AddParameter("Force", true);
                loginImport.Invoke();
            }

            // Import module — triggers AzclipsServiceProvider.Initialize(PowerShell) via Login.
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
            AzclipsServiceProvider.Dispose();
            runspace.Close();
            runspace.Dispose();
        }
    }
}
