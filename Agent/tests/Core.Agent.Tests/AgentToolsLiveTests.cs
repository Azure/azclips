using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Live smoke test for the seven <c>az agent tool</c> commands against a REAL
/// installed azclips binary. It is NOT a CI test — it needs an installed package
/// and an Azure login, so it self-skips unless a binary is resolvable. Run it by
/// hand after installing a fresh package to confirm all seven tools load and run
/// (this is exactly the class of failure — an agent dll left unloadable by a
/// packaging-layout change — that unit tests can't see).
///
/// Binary resolution: env var <c>AZCLIPS_LIVE_AZ</c> first, else the first <c>az</c>
/// on PATH. Skips (does not fail) when none is found.
///
/// Read-only by design: it never logs out, removes profiles, or mutates user
/// state — only the query/help tools are exercised.
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
        ["agent", "tool", "cache", "refresh"],
        ["agent", "tool", "resource", "find", "--name-pattern", "zzz-nonexistent"],
        ["agent", "tool", "resource", "show", "--name", "zzz-nonexistent", "--kind", "resource"],
        ["agent", "tool", "resource", "sku", "--vm-size", "Standard_D2s_v5"],
        ["agent", "tool", "resource", "tree", "--name-or-id", "zzz-nonexistent", "--kind", "subscription"],
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

        // The agent subprocess only spawns when agent.enabled=true and the
        // session style is oneshot (interactive needs a TTY we don't have here).
        await RunAsync(az!, "config", "set", "--key", "agent.enabled", "--value", "true");
        await RunAsync(az!, "config", "set", "--key", "agent.sessionStyle", "--value", "oneshot");

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

    private static Task<(int exit, string stdout, string stderr)> RunAsync(string az, params string[] args)
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

        var proc = new Process { StartInfo = psi };
        var outBuf = new StringBuilder();
        var errBuf = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) outBuf.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) errBuf.AppendLine(e.Data); };

        var tcs = new TaskCompletionSource<(int, string, string)>();
        proc.EnableRaisingEvents = true;
        proc.Exited += (_, _) =>
        {
            proc.WaitForExit(); // flush async buffers
            tcs.TrySetResult((proc.ExitCode, outBuf.ToString(), errBuf.ToString()));
            proc.Dispose();
        };
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        return tcs.Task;
    }
}
