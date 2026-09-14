using System.Diagnostics;
using Tests.Recording;
using Tests.Common.Utilities;

namespace Scenario.Tests;

internal sealed record ScriptRunResult(int ExitCode, string StdOut, string StdErr, int EntriesConsumed, int EntriesTotal);

/// <summary>
/// Runs a scenario script in a real shell (pwsh for .ps1, bash for .sh) against the built az
/// binary, with the recording hook active. The environment layer gives each run an isolated
/// azclips home (via AZCLIPS_HOME) seeded with a placeholder profile, plus a private copy of
/// the recording so the consumption ledger never touches the committed file.
/// </summary>
internal static class ScriptScenarioRunner
{
#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif

    public static ScriptRunResult RunPlayback(string scriptName, string recordingPath)
    {
        var repoRoot = RepoLocator.GetRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "tests", "Scenario.Tests", "Scripts", scriptName);
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Scenario script not found: {scriptPath}");
        }

        var home = Path.Combine(Path.GetTempPath(), "azclips-scenario-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        try
        {
            var recordingCopy = Path.Combine(home, "recording.json");
            File.Copy(recordingPath, recordingCopy);
            SeedPlaybackProfile(home);

            var (shell, arguments) = scriptName.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)
                ? ("pwsh", new[] { "-NoProfile", "-File", scriptPath })
                : (ResolveBash(), [scriptPath]);

            var psi = new ProcessStartInfo
            {
                FileName = shell,
                // scripts refer to repo-relative paths
                WorkingDirectory = repoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var argument in arguments)
            {
                psi.ArgumentList.Add(argument);
            }

            // Forward slashes keep the same value valid for pwsh, bash, and the CLI itself.
            psi.Environment["AZCLIPS_HOME"] = home.Replace('\\', '/');
            psi.Environment[RecordingHook.RecordingPathVariable] = recordingCopy.Replace('\\', '/');
            psi.Environment["AZCLIPS_TEST_AZ"] = ResolveCliBinary(repoRoot).Replace('\\', '/');
            psi.Environment["AZCLIPS_PS_MODULES"] = ResolvePsModuleRoot(repoRoot).Replace('\\', '/');
            // The product has no test awareness; the runtime injects the recording assembly
            // into every spawned az and pwsh process.
            psi.Environment["DOTNET_STARTUP_HOOKS"] = typeof(RecordingHook).Assembly.Location;
            // Script tests always replay; an ambient record-mode run must not leak in.
            psi.Environment.Remove(RecorderModeResolver.EnvironmentVariable);

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException($"Failed to start {shell}.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(120_000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"Scenario script {scriptName} did not finish within two minutes.");
            }

            return new ScriptRunResult(
                process.ExitCode,
                stdout.GetAwaiter().GetResult(),
                stderr.GetAwaiter().GetResult(),
                new ConsumptionLedger(recordingCopy + ".consumed").Read().Count,
                SessionRecord.Load(recordingCopy).Entries.Count);
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch { }
        }
    }

    // On Windows, PATH's bash is often the System32 WSL relay, which fails without a distro.
    // Git's bash is present wherever the repo was cloned, so prefer it; null means no usable
    // bash, and bash scenarios skip instead of failing.
    internal static string? TryResolveBash()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "bash";
        }

        string?[] roots =
        [
            Environment.GetEnvironmentVariable("ProgramFiles"),
            Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
            Environment.GetEnvironmentVariable("LocalAppData") is { } local ? Path.Combine(local, "Programs") : null,
        ];
        foreach (var root in roots)
        {
            if (root is not null && Path.Combine(root, "Git", "bin", "bash.exe") is var candidate && File.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    private static string ResolveBash()
        => TryResolveBash()
           ?? throw new InvalidOperationException("No usable bash found; on Windows only Git's bash works, the System32 relay needs WSL.");

    private static void SeedPlaybackProfile(string home)
    {
        File.WriteAllText(Path.Combine(home, "profiles.json"), """
            {
              "playback": {
                "name": "playback",
                "tenantId": "00000000-0000-0000-0000-000000000000",
                "subscriptionId": "00000000-0000-0000-0000-000000000000",
                "cloudName": "AzureCloud",
                "authMethod": 0,
                "userPrincipalName": "playback@example.com"
              }
            }
            """);
        File.WriteAllText(Path.Combine(home, "active-profile"), "playback");
    }

    // The build stages each PS module as a self-contained folder under
    // artifacts/<Configuration>/PS, so scripts import the staged module of the same
    // configuration the tests were built with instead of the source tree.
    private static string ResolvePsModuleRoot(string repoRoot)
    {
        var moduleRoot = Path.Combine(repoRoot, "artifacts", BuildConfiguration, "PS");
        return Directory.Exists(moduleRoot)
            ? moduleRoot
            : throw new InvalidOperationException($"Staged PS modules not found: {moduleRoot}. Run `dotnet build azclips.slnx` first.");
    }

    private static string ResolveCliBinary(string repoRoot)
    {
        var binary = Path.Combine(
            repoRoot, "artifacts", BuildConfiguration, "Core.Cli",
            OperatingSystem.IsWindows() ? "az.exe" : "az");
        return File.Exists(binary)
            ? binary
            : throw new InvalidOperationException($"CLI binary not found: {binary}. Run `dotnet build azclips.slnx` first.");
    }
}
