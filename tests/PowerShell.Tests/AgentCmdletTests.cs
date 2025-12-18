using System.Diagnostics;

namespace PowerShell.Tests;

/// <summary>
/// Integration tests for Resources PowerShell module with AI agent support.
/// Tests invoke pwsh as a subprocess to test real module loading.
/// </summary>
public class AgentCmdletTests
{

    private static string GetRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, ".git", "config")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? AppContext.BaseDirectory;
    }

    private static async Task<(int exitCode, string output)> RunPowerShellAsync(string command, int timeoutSeconds = 10)
    {
        var repoRoot = GetRepositoryRoot();
        var modulePath = Path.Combine(repoRoot, "src", "PS", "Resources", "Resources.psd1");
        
        var script = $"Import-Module '{modulePath}'; {command}";
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            Arguments = $"-NoProfile -Command \"{script.Replace("\"", "`\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (sender, e) => 
        {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (sender, e) => 
        {
            if (e.Data != null) errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var completed = await Task.Run(() => process.WaitForExit(timeoutSeconds * 1000));
        if (!completed)
        {
            process.Kill();
            throw new TimeoutException($"PowerShell command timed out after {timeoutSeconds} seconds");
        }

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();
        var fullOutput = output + error;

        return (process.ExitCode, fullOutput);
    }

    [Fact]
    public void Module_ShouldExist()
    {
        var repoRoot = GetRepositoryRoot();
        var modulePath = Path.Combine(repoRoot, "src", "PS", "Resources", "Resources.psd1");
        Assert.True(File.Exists(modulePath), $"Module should exist at: {modulePath}");
    }
}
