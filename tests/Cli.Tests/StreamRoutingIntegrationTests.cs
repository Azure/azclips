// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

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
        AssertConfigGetResult(stdout, "core.logging.level", "warn");
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
            "config", "get", "--key", "core.logging.level", "--query", "value");

        Assert.Equal(0, exit);
        // A --query narrowing a result to a scalar now uses clients.cli.output.simpleFormat
        // (default tsv) rather than the complex-result default (json) -- plain text, no quotes.
        Assert.Equal("warn", stdout.Trim());
        Assert.DoesNotContain("ERROR:", stderr);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupProfileLoad_AppliesDebugOverride_WhenProfileFileExists(bool debugOverride)
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
            var args = new List<string> { "config", "get", "--key", "core.logging.level" };
            if (debugOverride)
            {
                args.Insert(0, "--debug");
            }
            var (exit, stdout, stderr) = await RunCliInHomeAsync(homeDirectory, args.ToArray());

            Assert.Equal(0, exit);
            AssertConfigGetResult(stdout, "core.logging.level", "warn");
            if (debugOverride)
            {
                Assert.Contains("VERBOSE: [ProfileService] Loading profiles from", stderr);
            }
            else
            {
                Assert.DoesNotContain("[ProfileService] Loading profiles from", stderr);
            }
            Assert.DoesNotContain("VERBOSE:", stdout);
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
            "--output", "json", "config", "get", "--key", "core.logging.level", "--query", "value");

        Assert.Equal(0, exit);
        Assert.Equal("\"warn\"" + Environment.NewLine, stdout);
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
    public async Task InvalidQuery_RemainsText_WhenJsonOutputIsRequested()
    {
        var (exit, stdout, stderr) = await RunCliAsync("--output", "json", "logout", "--query", "[[[");

        Assert.Equal(2, exit);
        Assert.Empty(stdout);
        Assert.Contains("ERROR: [UsageError]", stderr);
        Assert.Contains("--query expression", stderr);
        Assert.DoesNotContain("\"error\"", stderr);
    }

    [Fact]
    public async Task InvalidInput_AgentDisabled_EmitsTypedErrorAndExitsTwo()
    {
        // Agent fallback is off by default, so the agent-eligible InvalidInput falls through to the typed error.
        var (exit, stdout, stderr) = await RunCliAsync("config", "set", "--key", "bogus..key", "--value", "x");

        Assert.Equal(2, exit);
        Assert.Empty(stdout);
        Assert.Contains("ERROR: [InvalidInput]", stderr);
    }

    [Fact]
    public async Task DebugFlag_EmitsDebugToStderr_LeavesStdoutClean()
    {
        var (exit, stdout, stderr) = await RunCliAsync("--debug", "config", "get", "--key", "core.logging.level");

        Assert.Equal(0, exit);
        AssertConfigGetResult(stdout, "core.logging.level", "warn");
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
    public async Task MissingRequiredParameters_EmitsTextError_WhenJsonOutputIsRequested()
    {
        var (exit, stdout, stderr) = await RunCliAsync("--output", "json", "vm", "create");

        Assert.Equal(2, exit);
        Assert.Empty(stdout);
        Assert.Contains("ERROR: [UsageError]", stderr);
        Assert.Contains("'--name'", stderr);
        Assert.Contains("az vm create --help", stderr);
        Assert.DoesNotContain("\"error\"", stderr);
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
    public async Task ForceColor_KeepsJsonResultAnsiFree_OnRedirectedStdout()
    {
        var (exit, stdout, _) = await RunCliWithEnvAsync(
            new Dictionary<string, string> { ["FORCE_COLOR"] = "1" },
            "config", "get", "--key", "core.logging.level");

        Assert.Equal(0, exit);
        Assert.DoesNotContain('\u001b', stdout);
        AssertConfigGetResult(stdout, "core.logging.level", "warn");
    }

    [Fact]
    public async Task ForceColor_ColorsJsoncResult_OnRedirectedStdout()
    {
        var (exit, stdout, _) = await RunCliWithEnvAsync(
            new Dictionary<string, string> { ["FORCE_COLOR"] = "1" },
            "--output", "jsonc", "config", "get", "--key", "core.logging.level");

        Assert.Equal(0, exit);
        Assert.Contains('\u001b', stdout);
        AssertConfigGetResult(StripAnsi(stdout), "core.logging.level", "warn");
    }

    [Fact]
    public async Task NoColor_RendersJsoncAsPlainJson_EvenWhenColorIsForced()
    {
        var (exit, stdout, _) = await RunCliWithEnvAsync(
            new Dictionary<string, string> { ["NO_COLOR"] = "1", ["FORCE_COLOR"] = "1" },
            "--output", "jsonc", "config", "get", "--key", "core.logging.level");

        Assert.Equal(0, exit);
        Assert.DoesNotContain('\u001b', stdout);
        AssertConfigGetResult(stdout, "core.logging.level", "warn");
    }

    [Fact]
    public async Task ForceColor_KeepsTextErrorAnsiFree_OnRedirectedStderr()
    {
        var (exit, stdout, stderr) = await RunCliWithEnvAsync(
            new Dictionary<string, string> { ["FORCE_COLOR"] = "1" },
            "--output", "json", "vm", "create");

        Assert.Equal(2, exit);
        Assert.Empty(stdout);
        Assert.DoesNotContain('\u001b', stderr);
        Assert.Contains("ERROR: [UsageError]", stderr);
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
                "config", "get", "--key", "core.logging.level", "--query", "value");

            Assert.Equal(0, exit);
            Assert.Equal("\"warn\"" + Environment.NewLine, stdout);
            Assert.DoesNotContain("ERROR:", stderr);
        }
        finally
        {
            try { Directory.Delete(homeDirectory, recursive: true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Version_AcceptsCommentsAndTrailingCommas_WithOrWithoutExplicitOutput(
        bool trailingComma, bool explicitOutput)
    {
        var json = trailingComma
            ? """{"telemetry":{"enabled":false},"agent":{"enabled":false},}"""
            : "// user configuration\n" + """{"telemetry":{"enabled":false},"agent":{"enabled":false}}""";
        var args = explicitOutput ? new[] { "--version", "--output", "json" } : new[] { "--version" };
        var (exit, stdout, stderr) = await RunCliWithConfigAsync(json, args);

        Assert.Equal(0, exit);
        Assert.Contains("AzCli", stdout);
        Assert.Contains("Runtime", stdout);
        Assert.DoesNotContain("ERROR:", stderr);
    }

    [Theory]
    [InlineData("json", false)]
    [InlineData("yaml", false)]
    [InlineData("yaml", true)]
    public async Task ConfigListGet_HonorsFlatColonConfiguredFormat_AndExplicitOverride(string format, bool explicitOutput)
    {
        var json = $$"""
            {
              // Source lookup accepts the same JSON syntax as options binding.
              "telemetry":{"enabled":false},
              "AGENT:EXCLUDEDCOMMANDS":"Login,profile show",
              "CLIENTS:CLI:OUTPUT:FORMAT":"{{format}}",
            }
            """;
        var args = new List<string> { "config", "get", "--key", "AGENT:EXCLUDEDCOMMANDS" };
        if (explicitOutput)
            args.AddRange(["--output", "json"]);
        var (exit, stdout, stderr) = await RunCliWithConfigAsync(json, args.ToArray());

        Assert.Equal(0, exit);
        Assert.DoesNotContain("ERROR:", stderr);
        if (format == "json" || explicitOutput)
        {
            var entries = JsonNode.Parse(stdout)!.AsArray();
            Assert.Equal(new[] { "Login", "profile show" }, entries.Select(n => n!.GetValue<string>()));
        }
        else
        {
            Assert.Equal("- Login\n- profile show", stdout.Replace("\r\n", "\n").Trim());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfigListGet_BareDefaultAndQuery_UseRealHostRendering(bool query)
    {
        const string json = """{"telemetry":{"enabled":false},"agent":{"excludedCommands":"Login,profile show"}}""";
        var args = new List<string> { "config", "get", "--key", "agent.excludedCommands" };
        if (query)
            args.AddRange(["--query", "[1]"]);
        var (exit, stdout, stderr) = await RunCliWithConfigAsync(json, args.ToArray());

        Assert.Equal(0, exit);
        Assert.DoesNotContain("ERROR:", stderr);
        Assert.Equal(query ? "profile show" : "agent.excludedCommands [global config]\n  - Login\n  - profile show",
            stdout.Replace("\r\n", "\n").Trim());
    }

    [Theory]
    [InlineData("""{"AGENT":{"EXCLUDEDCOMMANDS":"Login,logout","enabled":false}}""")]
    [InlineData("""{"agent":{"ExcludedCommands":"Login,logout","enabled":false}}""")]
    [InlineData("""{"AGENT.EXCLUDEDCOMMANDS":"Login,logout","agent:enabled":false}""")]
    [InlineData("""{"AGENT:EXCLUDEDCOMMANDS":"Login,logout","agent:enabled":false}""")]
    public async Task ConfigMutations_PreserveCasedNestedAndFlatKeys_AndRemainReadable(string stored)
    {
        var home = Directory.CreateTempSubdirectory("azclips-config-key-tests-").FullName;
        var config = JsonNode.Parse(stored)!.AsObject();
        config["telemetry"] = new JsonObject { ["enabled"] = false };
        var path = Path.Combine(home, "config.json");
        await File.WriteAllTextAsync(path, config.ToJsonString());
        try
        {
            var (addExit, addOutput, addError) = await RunCliInHomeAsync(home,
                "config", "set", "--key", "agent.excludedCommands", "--add", "profile");
            Assert.Equal(0, addExit);
            Assert.Contains("Login,logout,profile", addOutput);
            Assert.DoesNotContain("ERROR:", addError);
            await AssertStoredListAsync("Login", "logout", "profile");

            var (removeExit, removeOutput, removeError) = await RunCliInHomeAsync(home,
                "config", "set", "--key", "AGENT:EXCLUDEDCOMMANDS", "--remove", "az LOGIN");
            Assert.Equal(0, removeExit);
            Assert.Contains("logout,profile", removeOutput);
            Assert.DoesNotContain("ERROR:", removeError);
            await AssertStoredListAsync("logout", "profile");

            var (setExit, _, setError) = await RunCliInHomeAsync(home,
                "config", "set", "--key", "agent.excludedCommands", "--value", "replacement");
            Assert.Equal(0, setExit);
            Assert.DoesNotContain("ERROR:", setError);
            await AssertStoredListAsync("replacement");

            var (deleteExit, _, deleteError) = await RunCliInHomeAsync(home,
                "config", "delete", "--key", "AGENT:EXCLUDEDCOMMANDS");
            Assert.Equal(0, deleteExit);
            Assert.DoesNotContain("ERROR:", deleteError);
            await AssertStoredListAsync();
            var (otherExit, otherOutput, _) = await RunCliInHomeAsync(home,
                "config", "get", "--key", "agent.enabled");
            Assert.Equal(0, otherExit);
            Assert.Contains("\"source\": \"global config\"", otherOutput);
            Assert.Contains("\"value\": false", otherOutput);
            Assert.False(JsonNode.Parse(await File.ReadAllTextAsync(path))!["telemetry"]!["enabled"]!.GetValue<bool>());
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }

        async Task AssertStoredListAsync(params string[] expected)
        {
            var (exit, stdout, stderr) = await RunCliInHomeAsync(home,
                "config", "get", "--key", "agent.excludedCommands", "--output", "json");
            Assert.Equal(0, exit);
            Assert.DoesNotContain("ERROR:", stderr);
            Assert.Equal(expected, JsonNode.Parse(stdout)!.AsArray().Select(n => n!.GetValue<string>()));
            var raw = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            var keys = EnumerateLogicalKeys(raw).ToArray();
            Assert.Equal(expected.Length == 0 ? 0 : 1,
                keys.Count(key => key.Equals("agent.excludedCommands", StringComparison.OrdinalIgnoreCase)));
            Assert.Equal(keys.Length, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    [Fact]
    public async Task ConfigListMutations_ConcurrentProcesses_PreserveAllOperations()
    {
        for (var wave = 0; wave < 3; wave++)
        {
            var home = Directory.CreateTempSubdirectory("azclips-concurrent-config-tests-").FullName;
            var path = Path.Combine(home, "config.json");
            await File.WriteAllTextAsync(path,
                """{"AGENT:EXCLUDEDCOMMANDS":"seed","agent:enabled":true,"telemetry":{"enabled":false}}""");
            var env = new Dictionary<string, string>
            {
                ["AZCLIPS_IN_AGENT_MODE"] = "1",
                ["AZURE_CONFIG_DIR"] = home,
                ["HOME"] = home,
                ["USERPROFILE"] = home,
                ["LOCALAPPDATA"] = home,
                ["APPDATA"] = home,
                ["DOTNET_CLI_HOME"] = home
            };
            try
            {
                var additions = Enumerable.Range(1, 12)
                    .Select(n => new[] { "config", "set", "--key", "AGENT:EXCLUDEDCOMMANDS", "--add", $"entry-{n}" });
                await RunWaveAsync(additions);
                await AssertEntriesAsync(Enumerable.Range(1, 12));

                var mixed = Enumerable.Range(1, 6)
                    .Select(n => new[] { "config", "set", "agent.excludedCommands", "--remove", $"entry-{n}" })
                    .Concat(Enumerable.Range(13, 6)
                        .Select(n => new[] { "config", "set", "agent.excludedCommands", "--add", $"entry-{n}" }));
                await RunWaveAsync(mixed);
                await AssertEntriesAsync(Enumerable.Range(7, 12));

                var stored = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
                Assert.Single(EnumerateLogicalKeys(stored),
                    key => key.Equals("agent.excludedCommands", StringComparison.OrdinalIgnoreCase));
                Assert.True(stored["agent:enabled"]!.GetValue<bool>());
                Assert.False(stored["telemetry"]!["enabled"]!.GetValue<bool>());
                Assert.Empty(Directory.GetFiles(home, "config.*.tmp"));
            }
            finally
            {
                Directory.Delete(home, recursive: true);
            }

            async Task RunWaveAsync(IEnumerable<string[]> mutations)
            {
                // Start every writer before awaiting, alongside unlocked snapshot readers.
                var commands = mutations.Concat(Enumerable.Range(0, 3)
                    .Select(_ => new[] { "config", "get", "agent.excludedCommands", "--output", "json" }));
                var results = await Task.WhenAll(commands.Select(args => RunCliInHomeWithEnvAsync(home, env, args)));
                Assert.All(results, result =>
                    Assert.True(result.exit == 0, $"Wave {wave}: exit {result.exit}; {result.stderr}"));
                foreach (var reader in results.Skip(12))
                {
                    Assert.NotNull(JsonNode.Parse(reader.stdout)!.AsArray());
                }
            }

            async Task AssertEntriesAsync(IEnumerable<int> numbers)
            {
                var (exit, stdout, stderr) = await RunCliInHomeWithEnvAsync(home, env,
                    "config", "get", "agent.excludedCommands", "--output", "json");
                Assert.True(exit == 0, stderr);
                var actual = JsonNode.Parse(stdout)!.AsArray().Select(node => node!.GetValue<string>()).ToArray();
                var expected = numbers.Select(n => $"entry-{n}").Prepend("seed").ToArray();
                Assert.Equal("seed", actual[0]);
                Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
            }
        }
    }

    private static IEnumerable<string> EnumerateLogicalKeys(JsonObject root, string prefix = "")
    {
        foreach (var property in root)
        {
            var key = (prefix.Length == 0 ? property.Key : $"{prefix}.{property.Key}").Replace(':', '.');
            if (property.Value is JsonObject child)
            {
                foreach (var childKey in EnumerateLogicalKeys(child, key))
                    yield return childKey;
            }
            else
            {
                yield return key;
            }
        }
    }

    private static async Task<(int exit, string stdout, string stderr)> RunCliWithConfigAsync(string json, params string[] args)
    {
        var home = Directory.CreateTempSubdirectory("azclips-config-read-tests-").FullName;
        await File.WriteAllTextAsync(Path.Combine(home, "config.json"), json);
        try
        {
            return await RunCliInHomeAsync(home, args);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    private static Task<(int exit, string stdout, string stderr)> RunCliAsync(params string[] args)
        => RunCliWithEnvAsync(null, args);

    private static string StripAnsi(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, "\u001b\\[[0-9;]*m", string.Empty);

    private static async Task<(int exit, string stdout, string stderr)> RunCliWithEnvAsync(
        IReadOnlyDictionary<string, string>? env,
        params string[] args)
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
            return await RunCliInHomeWithEnvAsync(homeDirectory, env, args);
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

    private static void AssertConfigGetResult(string stdout, string expectedKey, string expectedValue)
    {
        using var document = System.Text.Json.JsonDocument.Parse(stdout);
        var entry = document.RootElement;

        Assert.Equal(expectedKey, entry.GetProperty("key").GetString());
        Assert.Equal(expectedValue, entry.GetProperty("value").GetString());
    }

    /// <summary>Runs the CLI against an already-prepared, caller-owned <paramref name="homeDirectory"/> -- lets a test issue multiple invocations (e.g. <c>config set</c> then <c>config get</c>) against the same persisted config.</summary>
    private static Task<(int exit, string stdout, string stderr)> RunCliInHomeAsync(string homeDirectory, params string[] args)
        => RunCliInHomeWithEnvAsync(homeDirectory, null, args);

    private static async Task<(int exit, string stdout, string stderr)> RunCliInHomeWithEnvAsync(
        string homeDirectory,
        IReadOnlyDictionary<string, string>? env,
        params string[] args)
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
        // Ambient color vars would otherwise leak into every assertion.
        psi.Environment.Remove("NO_COLOR");
        psi.Environment.Remove("FORCE_COLOR");
        foreach (var (name, value) in env ?? new Dictionary<string, string>())
        {
            psi.Environment[name] = value;
        }
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
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await proc.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            proc.Kill(entireProcessTree: true);
            await proc.WaitForExitAsync();
            throw new TimeoutException($"CLI process {proc.Id} exceeded 60 seconds.");
        }
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
