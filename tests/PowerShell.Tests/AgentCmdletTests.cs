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

    private static async Task<string> RunUntilAgentStartsAsync(string command, int timeoutSeconds = 30)
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
        var agentStarted = false;

        process.OutputDataReceived += (sender, e) => 
        {
            if (e.Data != null) 
            {
                outputBuilder.AppendLine(e.Data);
                if (e.Data.Contains("Analyzing your request") || 
                    e.Data.Contains("🤖") ||
                    e.Data.Contains("Agent started"))
                {
                    agentStarted = true;
                }
            }
        };
        process.ErrorDataReceived += (sender, e) => 
        {
            if (e.Data != null) 
            {
                errorBuilder.AppendLine(e.Data);
                if (e.Data.Contains("Analyzing your request") || 
                    e.Data.Contains("🤖") ||
                    e.Data.Contains("Agent started"))
                {
                    agentStarted = true;
                }
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var startTime = DateTime.UtcNow;
        while (!agentStarted && (DateTime.UtcNow - startTime).TotalSeconds < timeoutSeconds)
        {
            await Task.Delay(100);
            if (process.HasExited) break;
        }

        if (!process.HasExited)
        {
            process.Kill();
        }

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();
        return output + error;
    }

    [Fact]
    public void Module_ShouldExist()
    {
        var repoRoot = GetRepositoryRoot();
        var modulePath = Path.Combine(repoRoot, "src", "PS", "Resources", "Resources.psd1");
        Assert.True(File.Exists(modulePath), $"Module should exist at: {modulePath}");
    }

    [Fact]
    public async Task InvokeAzAIFunction_ShouldBeAvailable()
    {
        var (exitCode, output) = await RunPowerShellAsync("Get-Command Invoke-AzAI");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("Invoke-AzAI", output);
    }

    [Fact]
    public async Task AskAlias_ShouldBeAvailable()
    {
        var (exitCode, output) = await RunPowerShellAsync("Get-Alias ask");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("Invoke-AzAI", output);
    }

    [Fact]
    public async Task PleaseAlias_ShouldBeAvailable()
    {
        var (exitCode, output) = await RunPowerShellAsync("Get-Alias please");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("Invoke-AzAI", output);
    }

    [Fact]
    public async Task PlsAlias_ShouldBeAvailable()
    {
        var (exitCode, output) = await RunPowerShellAsync("Get-Alias pls");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("Invoke-AzAI", output);
    }

    [Fact]
    public async Task Agent_NaturalLanguage_ShouldInvokeAgent()
    {
        var output = await RunUntilAgentStartsAsync("ask how do I list resource groups");

        // Verify the AI agent responded with a relevant PowerShell command
        Assert.Contains("resourcegroup", output.ToLowerInvariant());
    }
}
