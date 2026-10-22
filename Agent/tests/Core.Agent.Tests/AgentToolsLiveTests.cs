using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Live smoke test for the <c>az agent tool</c> commands against a REAL installed
/// azclips binary, exercising BOTH assembly-load paths that packaging can break
/// independently: the CLI extension AssemblyLoadContext (the <c>agent tool</c>
/// commands) and the agent subprocess spawned by a one-shot fallback. It is NOT
/// a CI test — it needs an installed package and an Azure login, so it is
/// Skip-by-default. Run it by hand after installing a fresh package to confirm
/// every tool loads and runs (the class of failure — an agent dll left
/// unloadable by a packaging-layout change — that unit tests can't see).
///
/// Coverage: the seven distinct tools (env, help, resource
/// find/show/sku/tree/refresh); <c>ToolInvocations</c> has eight entries because
/// <c>help</c> is exercised both with and without <c>--name</c>.
///
/// Binary resolution: env var <c>AZCLIPS_LIVE_AZ</c> first, else the first <c>az</c>
/// on PATH.
///
/// Side effects: the tool invocations are read-only, but the one-shot subprocess
/// test flips <c>agent.recovery.trigger</c>/<c>agent.recovery.style</c> to spawn the agent —
/// it snapshots and restores those config values in a finally block so a run
/// leaves the installed azclips config unchanged. It never logs out or touches
/// profiles.
/// </summary>
public class AgentToolsLiveTests
{
    // Every command under `az agent tool`. Kept in one place so the list stays
    // in lockstep with the actual tool surface.
    private static readonly string[][] ToolInvocations =
    [
        ["agent", "tool", "env"],
        ["agent", "tool", "help"],
        ["agent", "tool", "help", "--name", "profile show"],
        ["agent", "tool", "resource", "refresh"],
        ["agent", "tool", "resource", "find", "--name", "zzz-nonexistent"],
        ["agent", "tool", "resource", "show", "--name", "zzz-nonexistent", "--kind", "resource"],
        ["agent", "tool", "resource", "sku", "--vm-size", "Standard_D2s_v5"],
        ["agent", "tool", "resource", "tree", "--name", "zzz-nonexistent", "--kind", "subscription"],
    ];

    [Fact(Skip = "Live smoke test — needs an installed azclips package + Azure login. Run by hand after installing a fresh package: remove the Skip (or run with a filter) and set AZCLIPS_LIVE_AZ or have az on PATH.")]
    public async Task AllAgentTools_LoadAndRun_AgainstLiveBinary()
    {
        var az = ResolveAzBinary();
        Assert.True(az is not null,
            "No azclips binary found. Set AZCLIPS_LIVE_AZ to an installed az(.exe), or put one on PATH, then re-run this live test.");

        // Preflight: this test needs an active login (the resource/cache tools
        // hit ARM). Check once up front via `agent tool env` and give a clear
        // instruction instead of letting each tool fail opaquely.
        var (envExit, envOut, envErr) = await RunAsync(az!, "agent", "tool", "env");
        AssertNoAssemblyLoadError("agent tool env", envOut, envErr);
        Assert.True(envExit == 0, $"`agent tool env` failed (exit {envExit}). Is the package intact?\n{envOut}\n{envErr}");

        var signedIn = TryReadSignedIn(envOut);
        Assert.True(signedIn == true,
            "Not signed in. Run `az login --subscription-id <sub>` against this binary, then re-run this live test.");

        // Exercise every tool. The bar is: it LOADS and RUNS — no assembly-load
        // failure, no 'command not found'. A tool returning "no results" or a
        // clean non-zero (e.g. empty cache) is fine; a load/crash is not.
        var failures = new List<string>();
        foreach (var argv in ToolInvocations)
        {
            var (_, stdout, stderr) = await RunAsync(az!, argv);
            var label = "az " + string.Join(' ', argv);
            var combined = stdout + "\n" + stderr;
            if (HasAssemblyLoadError(combined) || HasCommandNotFound(combined))
                failures.Add($"{label}\n  -> {FirstLine(combined)}");
        }

        Assert.True(failures.Count == 0,
            "These agent tools failed to load/run against the live binary:\n" + string.Join("\n", failures));
    }

    // The OTHER load path: `az <failing-command>` spawns the azclips-agent
    // SUBPROCESS (hostfxr + azclips-agent.deps.json + additionalProbingPaths),
    // which is DISTINCT from the `agent tool` CLI-command path above. A packaging
    // layout that satisfies one loader can still break the other (that's exactly
    // the bug this pair guards against), so both are exercised together. one-shot
    // mode runs non-interactively and exits, so it's CI/live-runnable here.
    [Fact(Skip = "Live smoke test — needs an installed azclips package + Azure login. Run by hand after installing a fresh package: remove the Skip and set AZCLIPS_LIVE_AZ or have az on PATH.")]
    public async Task OneShotAgentSubprocess_LoadsAndRuns_AgainstLiveBinary()
    {
        var az = ResolveAzBinary();
        Assert.True(az is not null,
            "No azclips binary found. Set AZCLIPS_LIVE_AZ to an installed az(.exe), or put one on PATH, then re-run this live test.");

        // Snapshot the config keys we must flip, so we can restore the user's
        // installed config exactly — a post-install smoke test must not leave the
        // machine mutated. Restore happens in finally regardless of outcome.
        var savedTrigger = await SnapshotConfigAsync(az!, "agent.recovery.trigger");
        var savedStyle = await SnapshotConfigAsync(az!, "agent.recovery.style");
        try
        {
            // The agent subprocess only spawns when agent.recovery.trigger=auto and the
            // session style is oneshot (interactive needs a TTY we don't have here).
            await RunAsync(az!, "config", "set", "--key", "agent.recovery.trigger", "--value", "auto");
            await RunAsync(az!, "config", "set", "--key", "agent.recovery.style", "--value", "oneshot");

            var signedIn = TryReadSignedIn((await RunAsync(az!, "agent", "tool", "env")).stdout);
            Assert.True(signedIn == true,
                "Not signed in. Run `az login --subscription-id <sub>` against this binary, then re-run this live test.");

            // `profile show` (no args) fails with a structured error, which the CLI
            // funnels into the agent fallback — spawning azclips-agent. The bar is the
            // same: the subprocess LOADS (no 'assembly not found in
            // azclips-agent.deps.json') and produces agent output, not a load crash.
            var (_, stdout, stderr) = await RunAsync(az!, "profile", "show");
            var combined = stdout + "\n" + stderr;

            AssertNoAssemblyLoadError("az profile show (one-shot agent subprocess)", stdout, stderr);
            // Positive signal that the agent actually ran (rendered its help/plan),
            // rather than the CLI just printing the raw error and never spawning.
            Assert.True(combined.Contains("AI", StringComparison.OrdinalIgnoreCase) ||
                        combined.Contains("agent", StringComparison.OrdinalIgnoreCase) ||
                        combined.Contains("login", StringComparison.OrdinalIgnoreCase),
                $"The one-shot agent subprocess did not appear to run. Output:\n{combined}");
        }
        finally
        {
            await RestoreConfigAsync(az!, "agent.recovery.trigger", savedTrigger);
            await RestoreConfigAsync(az!, "agent.recovery.style", savedStyle);
        }
    }

    // (value, wasDefault) for a config key. wasDefault=true means the user hadn't
    // set it explicitly, so restoring means DELETE rather than set-back.
    private static async Task<(string? value, bool wasDefault)> SnapshotConfigAsync(string az, string key)
    {
        var (_, stdout, _) = await RunAsync(az, "config", "get", "--key", key, "--output", "json");
        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;
        var source = root.TryGetProperty("source", out var s) ? s.GetString() : null;
        Assert.True(source is not null,
            $"Could not read the current value of {key}; refusing to change configuration. Output:\n{stdout}");
        var value = root.TryGetProperty("value", out var v) ? v.ToString() : null;
        // "global config" / "local config" = explicitly set; anything else
        // (e.g. "default") means the user never set it.
        var wasDefault = !source!.Contains("config", StringComparison.OrdinalIgnoreCase);
        return (value, wasDefault);
    }

    private static async Task RestoreConfigAsync(string az, string key, (string? value, bool wasDefault) saved)
    {
        if (saved.wasDefault || saved.value is null)
            await RunAsync(az, "config", "delete", "--key", key);   // was never set → remove our override
        else
            await RunAsync(az, "config", "set", "--key", key, "--value", saved.value);
    }

    // env var override, else first `az` on PATH (the default install lands there).
    private static string? ResolveAzBinary()
    {
        var overridePath = Environment.GetEnvironmentVariable("AZCLIPS_LIVE_AZ");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            return overridePath;

        var exeNames = OperatingSystem.IsWindows() ? new[] { "az.exe", "az" } : new[] { "az" };
        var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var dir in pathDirs)
            foreach (var exe in exeNames)
            {
                var candidate = Path.Combine(dir.Trim(), exe);
                if (File.Exists(candidate)) return candidate;
            }
        return null;
    }

    private static bool? TryReadSignedIn(string envJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(envJson);
            return doc.RootElement.TryGetProperty("signedIn", out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? v.GetBoolean()
                : (bool?)null;
        }
        catch { return null; }
    }

    private static bool HasAssemblyLoadError(string text) =>
        text.Contains("Could not load file or assembly", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("assembly specified in the application dependencies manifest", StringComparison.OrdinalIgnoreCase);

    private static bool HasCommandNotFound(string text) =>
        text.Contains("is misspelled or not recognized", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("not found", StringComparison.OrdinalIgnoreCase) && text.Contains("command", StringComparison.OrdinalIgnoreCase);

    private static void AssertNoAssemblyLoadError(string label, string stdout, string stderr) =>
        Assert.False(HasAssemblyLoadError(stdout + "\n" + stderr),
            $"`{label}` hit an assembly-load failure — the agent extension's dlls aren't laid out where the loader looks:\n{FirstLine(stdout + stderr)}");

    private static string FirstLine(string s) =>
        s.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "(no output)";

    private static async Task<(int exit, string stdout, string stderr)> RunAsync(string az, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = az,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = new Process { StartInfo = psi };
        var outBuf = new StringBuilder();
        var errBuf = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) outBuf.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) errBuf.AppendLine(e.Data); };

        // Start, then attach the async readers, then await exit. Order matters:
        // BeginOutput/ErrorReadLine must be wired before the process can finish,
        // and awaiting WaitForExitAsync (rather than an Exited-callback that
        // disposes mid-read) avoids racing a fast-exiting child against the
        // reader setup. WaitForExitAsync also drains the async output streams.
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync();
        return (proc.ExitCode, outBuf.ToString(), errBuf.ToString());
    }
}
