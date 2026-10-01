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
        // A --query narrowing a result to a scalar now uses clients.cli.output.simpleFormat
        // (default tsv) rather than the complex-result default (json) -- plain text, no quotes.
        // Source is "environment" because RunCliAsync pins AZCLIPS_LOG_LEVEL for determinism.
        Assert.Equal("core.logging.level = warn [environment]", stdout.Trim());
        Assert.DoesNotContain("ERROR:", stderr);
    }

    [Fact]
    public async Task StartupProfileLoad_DoesNotLeakVerboseAtWarn_WhenProfileFileExists()
    {
        var homeDirectory = Directory.CreateTempSubdirectory("azclips-startup-log-tests-").FullName;
        File.WriteAllText(Path.Combine(homeDirectory, "config.json"), """{"telemetry":{"enabled":false}}""");
        File.WriteAllText(Path.Combine(homeDirectory, "profiles.json"), """
{
  "default": {
    "name": "default",
    "tenantId": "11111111-1111-1111-1111-111111111111",
    "subscriptionId": "22222222-2222-2222-2222-222222222222",
    "cloudName": "AzureCloud",
    "authMethod": "Browser"
  }
}
""");
        File.WriteAllText(Path.Combine(homeDirectory, "active-profile"), "default");

        try
        {
            var (exit, stdout, stderr) = await RunCliInHomeAsync(homeDirectory, "config", "get", "--key", "core.logging.level");

            Assert.Equal(0, exit);
            Assert.Contains("core.logging.level = warn", stdout);
            Assert.DoesNotContain("VERBOSE: [ProfileService] Loading profiles from", stderr);
            Assert.DoesNotContain("[ProfileService] Loading profiles from", stderr);
        }
        finally
        {
            try { Directory.Delete(homeDirectory, recursive: true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public async Task ConfigGet_AppliesQuery_HonorsExplicitOutputOverride_ForScalarResult()
    {
        var (exit, stdout, stderr) = await RunCliAsync(
            "--output", "json", "config", "get", "--key", "core.logging.level", "--query", "@");

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
    public async Task MissingRequiredParameters_EmitsParseableError_WhenStderrIsRedirected()
    {
        var (exit, stdout, stderr) = await RunCliAsync("--output", "json", "vm", "create");

        Assert.Equal(2, exit);
        Assert.Empty(stdout);
        // stderr is redirected here, so the help page is suppressed and the error stays machine-readable.
        using var document = System.Text.Json.JsonDocument.Parse(stderr);
        var error = document.RootElement.GetProperty("error");
        Assert.Equal("UsageError", error.GetProperty("code").GetString());
        var message = error.GetProperty("message").GetString();
        Assert.Contains("'--name'", message);
        Assert.Contains("az vm create --help", message);
        Assert.DoesNotContain("\\u0027", stderr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingRequiredParameters_DoesNotRenderHelpDocument_WhenRedirected()
    {
        var (_, stdout, stderr) = await RunCliAsync("vm", "create");

        Assert.Empty(stdout);
        Assert.DoesNotContain("# COMMAND", stderr);
        Assert.DoesNotContain("# ARGUMENTS", stderr);
    }

    [Fact]
    public async Task ConfigSet_SimpleFormat_ChangesQueryScalarRendering_EndToEnd()
    {
        // Exercises the real path -- az config set writes JSON, ConfigurationService binds it into
        // ClientsOptions, OutputFormatPolicy resolves it -- not just the in-memory unit tests, which
        // construct ClientsOptions/CliOutputOptions directly and never touch config persistence.
        var homeDirectory = Directory.CreateTempSubdirectory("azclips-stream-routing-tests-").FullName;
        File.WriteAllText(Path.Combine(homeDirectory, "config.json"), """{"telemetry":{"enabled":false}}""");
        try
        {
            var (setExit, _, setStderr) = await RunCliInHomeAsync(
                homeDirectory,
                "config", "set", "--key", "clients.cli.output.simpleFormat", "--value", "json", "--force");
            Assert.Equal(0, setExit);
            Assert.DoesNotContain("ERROR:", setStderr);

            var (exit, stdout, stderr) = await RunCliInHomeAsync(
                homeDirectory,
                "config", "get", "--key", "core.logging.level", "--query", "@");

            Assert.Equal(0, exit);
            Assert.StartsWith("\"core.logging.level = warn", stdout, StringComparison.Ordinal);
            Assert.EndsWith("\"" + Environment.NewLine, stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("ERROR:", stderr);
        }
        finally
        {
            try { Directory.Delete(homeDirectory, recursive: true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private static async Task<(int exit, string stdout, string stderr)> RunCliAsync(params string[] args)
    {
        // Isolate config/profiles/extensions from the developer's/CI's real ~/.azclips -- a
        // persisted override (e.g. clients.cli.output.simpleFormat) would otherwise change these
        // assertions. Fresh temp directory per invocation so parallel test runs never collide.
        var homeDirectory = Directory.CreateTempSubdirectory("azclips-stream-routing-tests-").FullName;
        // Telemetry off so Program.cs's detached --upload-telemetry child has nothing to read/write
        // under this home -- avoids racing our cleanup below rather than just tolerating the race.
        File.WriteAllText(Path.Combine(homeDirectory, "config.json"), """{"telemetry":{"enabled":false}}""");
        try
        {
            return await RunCliInHomeAsync(homeDirectory, args);
        }
        finally
        {
            // Best-effort: Program.cs's SpawnTelemetryUploader detaches an --upload-telemetry
            // child that inherits AZCLIPS_HOME and may still be reading/writing the trace
            // directory here, so an immediate recursive delete can race it (esp. on Windows,
            // where an open handle turns this into a flaky IOException, not a real failure).
            try { Directory.Delete(homeDirectory, recursive: true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>Runs the CLI against an already-prepared, caller-owned <paramref name="homeDirectory"/> -- lets a test issue multiple invocations (e.g. <c>config set</c> then <c>config get</c>) against the same persisted config.</summary>
    private static async Task<(int exit, string stdout, string stderr)> RunCliInHomeAsync(string homeDirectory, params string[] args)
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
        psi.Environment["AZCLIPS_HOME"] = homeDirectory;
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
