// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Cli.Tests;

/// <summary>
/// End-to-end integration tests that spawn the built <c>az</c> binary and assert
/// on the split between stdout and stderr. The in-process <c>ExecuteCli</c> fixture
/// bypasses <c>Program.Main</c> and <c>BuildStreams</c>, so it can't cover stream
/// routing — see PR <see href="https://github.com/Azure/azclips/pull/575">Azure/azclips#575</see>
/// (decision D9) for the rationale. These tests close that gap.
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
    public async Task ConfigGet_AppliesQuery_ToCommandOutput()
    {
        var (exit, stdout, stderr) = await RunCliAsync(
            "config", "get", "--key", "core.logging.level", "--query", "@");

        Assert.Equal(0, exit);
        Assert.StartsWith("\"core.logging.level = warn", stdout, StringComparison.Ordinal);
        Assert.EndsWith("\"" + Environment.NewLine, stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("ERROR:", stderr);
    }

    [Fact]
    public async Task Logout_InvalidQuery_FailsBeforeCommandExecution()
    {
        var (exit, stdout, stderr) = await RunCliAsync("logout", "--query", "[[[");

        Assert.Equal(2, exit);
        Assert.Empty(stdout);
        Assert.Contains("--query expression", stderr);
    }

    [Fact]
    public async Task InvalidQuery_UsesRequestedJsonErrorFormat()
    {
        var (exit, stdout, stderr) = await RunCliAsync("--output", "json", "logout", "--query", "[[[");

        Assert.Equal(2, exit);
        Assert.Empty(stdout);
        using var document = System.Text.Json.JsonDocument.Parse(stderr);
        Assert.Equal("UsageError", document.RootElement.GetProperty("error").GetProperty("code").GetString());
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
        Assert.Matches(@"\[(BundledExtensionLoader|InstalledExtensionLoader)\] Resolving extension", stderr);
        // Aggregate table header + total row from ExtensionLoadTracker.EmitSummary in finally.
        Assert.Contains("[ExtensionLoadTracker] Extension", stderr);
        Assert.Contains("[ExtensionLoadTracker] Total (", stderr);
    }

    [Fact]
    public async Task BareGroup_RendersGroupHelpToStdout()
    {
        var (exit, stdout, stderr) = await RunCliAsync("vm");

        Assert.Equal(0, exit);
        Assert.Contains("# GROUP", stdout);
        Assert.Contains("az vm:", stdout);
        Assert.DoesNotContain("ERROR:", stderr);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-help")]
    [InlineData("-?")]
    [InlineData("--?")]
    public async Task GroupHelpAlias_RendersGroupHelpToStdout(string helpToken)
    {
        var (exit, stdout, _) = await RunCliAsync("vm", helpToken);

        Assert.Equal(0, exit);
        Assert.Contains("az vm:", stdout);
    }

    [Fact]
    public async Task GroupWithUnknownFlag_KeepsCommandNotFound_AndRendersNoHelp()
    {
        var (exit, stdout, stderr) = await RunCliAsync("vm", "--list");

        Assert.Equal(4, exit);
        Assert.Empty(stdout);
        Assert.Contains("CommandNotFound", stderr);
    }

    [Fact]
    public async Task MissingRequiredParameters_KeepStdoutParseable_AndRenderHelpOnStderr()
    {
        var (exit, stdout, stderr) = await RunCliAsync("--output", "json", "vm", "create");

        Assert.Equal(2, exit);
        // Help is diagnostic here: stdout must stay usable for `| jq` even though the command failed.
        Assert.Empty(stdout);
        // JSON output escapes the quotes around --name, so match the surrounding text instead.
        Assert.Contains("Required parameter", stderr);
        Assert.Contains("is missing.", stderr);
        Assert.Contains("# COMMAND", stderr);
        Assert.Contains("az vm create:", stderr);
    }

    [Fact]
    public async Task MissingRequiredParameters_ReportErrorBeforeHelp()
    {
        var (_, _, stderr) = await RunCliAsync("vm", "create");

        var errorIndex = stderr.IndexOf("Required parameter", StringComparison.Ordinal);
        var helpIndex = stderr.IndexOf("# COMMAND", StringComparison.Ordinal);

        Assert.True(errorIndex >= 0 && helpIndex > errorIndex, "The validation error must precede the help page.");
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
        // Pin AZCLIPS_LOG_LEVEL=warn so ambient developer/CI env doesn't shift the effective level
        // and invalidate the routing assertions below. Tests that need Debug pass `--debug` explicitly;
        // `args > env` precedence means the flag still wins over this pin.
        psi.Environment["AZCLIPS_LOG_LEVEL"] = "warn";
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var proc = new Process { StartInfo = psi };
        proc.Start();
        // Drain stdout/stderr concurrently via ReadToEndAsync so we deterministically capture
        // trailing lines. Event-based capture (OutputDataReceived) can race the process exit
        // and drop the last few lines under CI scheduling. Same pattern as
        // tests/Benchmark/Startup/StartupBenchmarkBase.RunToExit.
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return (proc.ExitCode, stdout, stderr);
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
