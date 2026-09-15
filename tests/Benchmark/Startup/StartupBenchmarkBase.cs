// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Benchmark.PS;
using Tests.Common.Utilities;

namespace Benchmark.Startup;

/// <summary>
/// Shared setup for process-level startup benchmarks. Each [Benchmark] spawns the real
/// published az / pwsh as a fresh OS process with AZCLIPS_BENCH_RESOLVE_ONLY=true so the child
/// stops at the resolve-only seam (startup work done, command not executed).
/// </summary>
public abstract class StartupBenchmarkBase
{
#if DEBUG
    protected const string Configuration = "Debug";
#else
    protected const string Configuration = "Release";
#endif

    protected string RepoRoot { get; private set; } = null!;

    protected void InitializeCommon() => RepoRoot = RepoLocator.GetRepoRoot();

    private string CliPath()
    {
        var exe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "az.exe" : "az";
        return Path.Combine(RepoRoot, "artifacts", Configuration, "Core.Cli", exe);
    }

    /// <summary>
    /// Stage a PS module and its <c>RequiredModules</c> dependency closure via
    /// <see cref="PSManifestStager"/>, returning the absolute path to the staged manifest.
    /// Unlike build/Set-ModuleConfiguration.ps1, this preserves <c>RequiredModules</c>, so importing
    /// a dependent module (e.g. Config) transitively imports Accounts and initializes the service
    /// provider. Staged fresh from source each run, so it is never stale (survives renames).
    /// Call from <c>[GlobalSetup]</c> so the staging I/O stays outside the measured region.
    /// </summary>
    protected string StageModule(string module) => PSManifestStager.StageModule(RepoRoot, module);

    /// <summary>ProcessStartInfo for an az CLI invocation, stopping at the seam.</summary>
    protected ProcessStartInfo CliStartInfo(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = CliPath(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        // "true" (not "1"): resolved through the configuration service into BenchmarkOptions.ResolveOnly,
        // whose bool binder accepts true/false only.
        psi.Environment["AZCLIPS_BENCH_RESOLVE_ONLY"] = "true";
        return psi;
    }

    /// <summary>ProcessStartInfo for a pwsh module-import + cmdlet invocation, stopping at the seam.</summary>
    protected ProcessStartInfo PsStartInfo(string modulePsd1, string cmdlet, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "pwsh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-Command");
        var call = args.Length == 0 ? cmdlet : $"{cmdlet} {string.Join(' ', args)}";
        psi.ArgumentList.Add($"Import-Module '{modulePsd1}'; {call} | Out-Null");
        psi.Environment["AZCLIPS_BENCH_RESOLVE_ONLY"] = "true";
        return psi;
    }

    /// <summary>Start the process, drain its streams, wait for exit, and assert exit code 0.</summary>
    protected static void RunToExit(ProcessStartInfo psi)
    {
        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException($"Process.Start returned null for {psi.FileName}");
        // Drain stdout/stderr concurrently to avoid deadlocks when either stream fills its buffer.
        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();
        p.WaitForExit();
        Task.WaitAll(stdoutTask, stderrTask);
        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Startup probe exited {p.ExitCode}: {psi.FileName} {string.Join(' ', psi.ArgumentList)}");
        }
    }
}
