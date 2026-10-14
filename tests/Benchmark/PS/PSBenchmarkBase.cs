// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Benchmark.Telemetry;
using BenchmarkDotNet.Attributes;
using Core.Abstractions;
using Core.DependencyInjection;
using Core.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using Perfolizer.Mathematics.OutlierDetection;

namespace Benchmark.PS;

[MemoryDiagnoser]
[InvocationCount(invocationCount: 1, unrollFactor: 1)]
[WarmupCount(5000)]
[IterationCount(5000)]
[Outliers(OutlierMode.RemoveAll)]
public abstract class PSBenchmarkBase : BenchmarkBase
{
    private static readonly ClientMetadata s_clientMetadata = new(
        ClientType.PowerShell,
        "1950a258-227b-4e31-a9cf-717495945fc2");

    /// <summary>
    /// The PowerShell module this benchmark exercises. Only this module and its
    /// <c>RequiredModules</c> dependency closure are staged for the run. Defaults to the
    /// extension named by the class's <c>[BenchmarkExtension]</c> attribute; override only if
    /// the module name differs from the extension name.
    /// </summary>
    protected virtual string ModuleName => ExtensionName;

    private string _psModuleManifestPath = null!;
    private Runspace _runspace = null!;
    private BenchmarkFileTelemetrySender _sender = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        InitializeCommon(s_clientMetadata);

        _psModuleManifestPath = PSManifestStager.StageModule(RepoRoot, ModuleName);

        VerifyAuthFixture();

        Console.WriteLine($"[Setup] PS benchmarks ready for '{ModuleName}' (stub HTTP, real MSAL cache IO)");

        InitializeTelemetryCommon();

        // The sender holds two paths and the stage listener, so it is constant for the whole run;
        // only what ConfigureExporter builds is per-iteration.
        _sender = new BenchmarkFileTelemetrySender(
            Stage,
            TelemetryArtifacts.WarmupDirectory,
            TelemetryArtifacts.ActualDirectory);
    }

    /// <summary>
    /// Registers the benchmark telemetry stack: the real TelemetryService, a sender that writes
    /// trace files, and the filter that drops module-import traces.
    /// </summary>
    /// <remarks>
    /// Runs every iteration because [IterationCleanup] disposes the container. ITelemetryProvider
    /// is omitted so the PSTelemetryProvider that Import-Module registers wins.
    /// </remarks>
    private void RegisterTelemetryCapture()
    {
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<ITelemetryService, TelemetryService>();
            services.AddSingleton<ITelemetrySender>(_sender);

            // Factory rather than by-type: ImportTraceFilter is internal and DI's constructor
            // selection wants a public one.
            services.AddSingleton<BaseProcessor<Activity>>(_ => new ImportTraceFilter());
        });
    }

    [IterationSetup]
    public void IterationSetup()
    {
        SeedProfileStore();
        RegisterBenchmarkStubs();
        RegisterTelemetryCapture();

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

            // Build the telemetry stack eagerly to ensure all telemetry dependencies have been resolved and are not resolved in the measured region.
            AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
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
        // A blocking drain since the FileExporter caps it at 500ms
        _sender?.DrainToDisk();

        CleanupCommon();

        _runspace?.Close();
        _runspace?.Dispose();
        _runspace = null!;
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        CleanupTelemetryCommon();
        CleanupGlobal();
    }

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
