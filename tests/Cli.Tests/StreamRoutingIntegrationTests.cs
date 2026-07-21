// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Cli.Tests;

/// <summary>
/// End-to-end integration tests that spawn the built <c>az</c> binary and assert
/// on the split between stdout and stderr. The in-process <c>ExecuteCli</c> fixture
/// bypasses <c>Program.Main</c> and <c>BuildStreams</c>, so it can't cover stream
/// routing (see decision D9 in <c>TEMP-PR-PLAN.md</c>). These tests close that gap.
/// </summary>
public class StreamRoutingIntegrationTests
{
    // Resolve artifacts against the configuration these tests were built with,
    // so the suite works whether the solution is built Debug (local/GitHub) or
    // Release (distribution pipeline).
#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif

    [Fact]
    public async Task WriteError_GoesToStderr_NotStdout()
    {
        var (exit, stdout, stderr) = await RunCliAsync("nonexistent-command-xyz");

        Assert.NotEqual(0, exit);
        Assert.DoesNotContain("ERROR:", stdout);
        Assert.Contains("ERROR:", stderr);
    }

    [Fact]
    public async Task ConfigGet_WritesResultToStdout_UnderWarnDefault()
    {
        var (exit, stdout, stderr) = await RunCliAsync("config", "get", "--key", "core.logging.level");

        Assert.Equal(0, exit);
        Assert.Contains("core.logging.level = warn", stdout);
        // Under the warn default, a plain get produces no diagnostic chatter on stderr —
        // including the ExtensionLoadTracker summary, which must stay gated behind --debug.
        Assert.DoesNotContain("DEBUG:", stderr);
        Assert.DoesNotContain("VERBOSE:", stderr);
        Assert.DoesNotContain("[ExtensionLoadTracker]", stderr);
    }

    [Fact]
    public async Task DebugFlag_EmitsDebugToStderr_LeavesStdoutClean()
    {
        var (exit, stdout, stderr) = await RunCliAsync("--debug", "config", "get", "--key", "core.logging.level");

        Assert.Equal(0, exit);
        Assert.Contains("core.logging.level = warn", stdout);
        Assert.Contains("DEBUG:", stderr);
        // stdout should still be pure result — no diagnostic leakage.
        Assert.DoesNotContain("DEBUG:", stdout);
        Assert.DoesNotContain("VERBOSE:", stdout);
        Assert.DoesNotContain("INFO:", stdout);
    }

    [Fact]
    public async Task DebugFlag_EmitsExtensionLoaderChainAndSummary()
    {
        var (exit, _, stderr) = await RunCliAsync("--debug", "config", "get", "--key", "core.logging.level");

        Assert.Equal(0, exit);
        // Per-load lines from the two loaders (at least one).
        Assert.Matches(@"\[(BuiltinExtensionLoader|MultiVersionExtensionLoader)\] Resolving extension", stderr);
        // Aggregate table header + total row from ExtensionLoadTracker.EmitSummary in finally.
        Assert.Contains("[ExtensionLoadTracker] Extension", stderr);
        Assert.Contains("[ExtensionLoadTracker] Total (", stderr);
    }

    private static async Task<(int exit, string stdout, string stderr)> RunCliAsync(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ResolveCliBinary(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var proc = new Process { StartInfo = psi };
        var outBuf = new StringBuilder();
        var errBuf = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) outBuf.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) errBuf.AppendLine(e.Data); };
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync();
        return (proc.ExitCode, outBuf.ToString(), errBuf.ToString());
    }

    private static string ResolveCliBinary()
    {
        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "az.exe" : "az";
        var cliBinary = Path.Combine(ResolveArtifactsDirectory(), "Core.Cli", exeName);

        if (!File.Exists(cliBinary))
        {
            throw new InvalidOperationException(
                $"CLI binary not found: {cliBinary}. Run `dotnet build azclips.slnx` first.");
        }

        return cliBinary;
    }

    private static string ResolveArtifactsDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "artifacts", BuildConfiguration);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"Could not locate 'artifacts/{BuildConfiguration}' above {AppContext.BaseDirectory}. " +
            $"Run `dotnet build azclips.slnx` to produce it.");
    }
}
