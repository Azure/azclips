using System.Diagnostics;
using Core.Abstractions;
using Core.Recording;
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
            SeedInstalledExtensions(home, repoRoot);

            var isPowerShellScript = scriptName.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase);
            var homeValue = home.Replace('\\', '/');
            var recordingValue = recordingCopy.Replace('\\', '/');
            var azPath = ResolveCliBinary(repoRoot);
            var bashPath = isPowerShellScript ? null : ResolveBash();
            var useGitBashPaths = bashPath is not null && IsGitBashPath(bashPath);
            var (shell, arguments) = isPowerShellScript
                ? ("pwsh", new[] { "-NoProfile", "-File", scriptPath })
                : (bashPath!,
                [
                    "-lc",
                    $"AZCLIPS_HOME={QuoteForBash(homeValue)} {RecordingHook.RecordingPathVariable}={QuoteForBash(recordingValue)} AZCLIPS_TEST_AZ={QuoteForBash(ToBashPath(azPath, useGitBashPaths))} exec {QuoteForBash(ToBashPath(scriptPath, useGitBashPaths))}"
                ]);

            var psi = new ProcessStartInfo
            {
                FileName = shell,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var argument in arguments)
            {
                psi.ArgumentList.Add(argument);
            }

            // Forward slashes keep the same value valid for pwsh, bash, and the CLI itself.
            psi.Environment["AZCLIPS_HOME"] = homeValue;
            psi.Environment[RecordingHook.RecordingPathVariable] = recordingValue;
            psi.Environment["AZCLIPS_TEST_AZ"] = !isPowerShellScript
                ? ToBashPath(azPath, useGitBashPaths)
                : azPath.Replace('\\', '/');
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

    private static void SeedInstalledExtensions(string home, string repoRoot)
    {
        var extensionsRoot = Path.Combine(home, HomePaths.ExtensionsDirectoryName);
        CopyExtensionPackage(repoRoot, extensionsRoot, "Microsoft.Azure.Azclips.Extensions.Resources");
        CopyExtensionPackage(repoRoot, extensionsRoot, "Microsoft.Azure.Azclips.Extensions.Compute");
    }

    private static void CopyExtensionPackage(string repoRoot, string extensionsRoot, string packageId)
    {
        var sourceRoot = Path.Combine(repoRoot, "artifacts", BuildConfiguration, packageId);
        if (!Directory.Exists(sourceRoot))
        {
            throw new InvalidOperationException($"Extension package output not found: {sourceRoot}. Run `dotnet build azclips.slnx` first.");
        }

        var version = Directory.EnumerateFiles(sourceRoot, $"{packageId}.*.nupkg", SearchOption.TopDirectoryOnly)
            .Select(path => TryGetPackageVersion(packageId, Path.GetFileName(path)))
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .OrderByDescending(version => version, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Could not resolve extension package version for {packageId} under {sourceRoot}.");

        var destinationRoot = Path.Combine(extensionsRoot, packageId, version);
        Directory.CreateDirectory(destinationRoot);
        foreach (var sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.TopDirectoryOnly))
        {
            File.Copy(sourcePath, Path.Combine(destinationRoot, Path.GetFileName(sourcePath)), overwrite: true);
        }
    }

    private static string? TryGetPackageVersion(string packageId, string fileName)
    {
        var prefix = packageId + ".";
        const string suffix = ".nupkg";
        return fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? fileName[prefix.Length..^suffix.Length]
            : null;
    }

    private static string QuoteForBash(string value) => $"'{value.Replace("'", "'\\''")}'";

    private static string ToBashPath(string path, bool useGitBashPaths)
    {
        var normalized = path.Replace('\\', '/');
        return OperatingSystem.IsWindows() &&
               normalized.Length >= 2 &&
               normalized[1] == ':' &&
               char.IsAsciiLetter(normalized[0])
            ? useGitBashPaths
                ? $"/{char.ToLowerInvariant(normalized[0])}{normalized[2..]}"
                : $"/mnt/{char.ToLowerInvariant(normalized[0])}{normalized[2..]}"
            : normalized;
    }

    private static string ResolveBash()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "bash";
        }

        string[] candidatePaths =
        [
            @"C:\Program Files\Git\bin\bash.exe",
            @"C:\Program Files\Git\usr\bin\bash.exe",
            @"C:\Program Files (x86)\Git\bin\bash.exe",
            @"C:\Program Files (x86)\Git\usr\bin\bash.exe",
        ];
        return candidatePaths.FirstOrDefault(File.Exists) ?? "bash";
    }

    private static bool IsGitBashPath(string path) =>
        path.Contains(@"\Git\", StringComparison.OrdinalIgnoreCase) ||
        path.Replace('\\', '/').Contains("/Git/", StringComparison.OrdinalIgnoreCase);

    private static string ResolveCliBinary(string repoRoot)
    {
        var testBinary = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "az.exe" : "az");
        if (File.Exists(testBinary))
        {
            return testBinary;
        }

        var binary = Path.Combine(
            repoRoot, "artifacts", BuildConfiguration, "Core.Cli",
            OperatingSystem.IsWindows() ? "az.exe" : "az");
        return File.Exists(binary)
            ? binary
            : throw new InvalidOperationException($"CLI binary not found: {binary}. Run `dotnet build azclips.slnx` first.");
    }
}
