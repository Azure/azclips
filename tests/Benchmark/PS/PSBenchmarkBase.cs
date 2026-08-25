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
    /// <c>RequiredModules</c> dependency closure are staged for the run. Defaults to the
    /// extension named by the class's <c>[BenchmarkExtension]</c> attribute; override only if
    /// the module name differs from the extension name.
    /// </summary>
    protected virtual string ModuleName => ExtensionName;

    private string _psModuleManifestPath = null!;
    private Runspace _runspace = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        InitializeCommon();

        _psModuleManifestPath = PSManifestStager.StageModule(RepoRoot, ModuleName);

        VerifyAuthFixture();

        Console.WriteLine($"[Setup] PS benchmarks ready for '{ModuleName}' (stub HTTP, real MSAL cache IO)");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        SeedProfileStore();
        RegisterBenchmarkStubs();

        // Prime the runspace and import the module. Module import execution is excluded from the measured region, so that the benchmark measures only the cmdlet invocation.
        _runspace = RunspaceFactory.CreateRunspace();
        try
        {
            _runspace.Open();

            using var import = PowerShell.Create();
            import.Runspace = _runspace;
            import.AddCommand("Import-Module").AddParameter("Name", _psModuleManifestPath).AddParameter("Force", true);
            import.Invoke();
            ThrowOnPowerShellErrors(import, "Import-Module");
        }
        catch
        {
            _runspace.Dispose();
            _runspace = null!;
            throw;
        }
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        CleanupCommon();

        _runspace?.Close();
        _runspace?.Dispose();
        _runspace = null!;
    }

    [GlobalCleanup]
    public void GlobalCleanup() => CleanupGlobal();

    protected object? RunPowerShellCommand(
        string command,
        params (string Name, object Value)[] parameters)
    {
        using var ps = PowerShell.Create();
        ps.Runspace = _runspace;
        ps.AddCommand(command);
        foreach (var (name, value) in parameters)
        {
            ps.AddParameter(name, value);
        }
        var results = ps.Invoke();

        // Invoke() only throws on terminating errors; fail loudly if the cmdlet wrote non-terminating errors to the error stream.
        ThrowOnPowerShellErrors(ps, command);

        return results.Count > 0 ? results[0] : null;
    }

    private static void ThrowOnPowerShellErrors(PowerShell ps, string context)
    {
        if (!ps.HadErrors)
        {
            return;
        }

        var errors = string.Join("; ", ps.Streams.Error.Select(e => e.ToString()));
        throw new InvalidOperationException(
            $"PowerShell '{context}' wrote {ps.Streams.Error.Count} non-terminating error(s): {errors}");
    }
}
