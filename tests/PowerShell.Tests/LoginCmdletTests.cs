using System.Diagnostics;

namespace PowerShell.Tests;

/// <summary>
/// Integration tests for Login PowerShell cmdlets.
/// Tests invoke pwsh as a subprocess to test real module loading.
/// </summary>
public class LoginCmdletTests
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
        var modulePath = Path.Combine(repoRoot, "bin", "Login", "Cirrus.Login.psd1");
        
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
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"PowerShell command timed out after {timeoutSeconds} seconds");
        }

        var output = outputBuilder.ToString() + errorBuilder.ToString();
        return (process.ExitCode, output);
    }

    private static async Task<string> RunUntilAgentStartsAsync(string command, int timeoutSeconds = 5)
    {
        var repoRoot = GetRepositoryRoot();
        var modulePath = Path.Combine(repoRoot, "bin", "Login", "Cirrus.Login.psd1");
        
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
        var agentStarted = false;

        process.OutputDataReceived += (sender, e) => 
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                
                // Agent started if we see "Thinking" indicator
                if (e.Data.Contains("Thinking"))
                {
                    agentStarted = true;
                }
            }
        };
        process.ErrorDataReceived += (sender, e) => 
        {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        // Wait for agent to start or timeout
        var startTime = DateTime.UtcNow;
        while (!agentStarted && (DateTime.UtcNow - startTime).TotalSeconds < timeoutSeconds)
        {
            if (process.HasExited)
                break;
            
            await Task.Delay(100);
        }

        // Kill process once we know agent started (or timeout)
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }

        return outputBuilder.ToString();
    }

    [Fact]
    public void Module_ShouldExist()
    {
        var repoRoot = GetRepositoryRoot();
        var modulePath = Path.Combine(repoRoot, "bin", "Login", "Cirrus.Login.psd1");
        Assert.True(File.Exists(modulePath), $"Module should exist at: {modulePath}");
    }

    [Fact]
    public async Task ConnectAzV2Account_ShouldBeAvailable()
    {
        var (exitCode, output) = await RunPowerShellAsync("Get-Command Connect-AzV2Account");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("Connect-AzV2Account", output);
    }

    [Fact]
    public async Task ConnectAzV2Account_ShouldAuthenticate()
    {
        var (exitCode, output) = await RunPowerShellAsync("Connect-AzV2Account | ConvertTo-Json");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("IsAuthenticated", output);
        Assert.Contains("true", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAzV2Context_ShouldReturnContext()
    {
        var (exitCode, output) = await RunPowerShellAsync("Connect-AzV2Account; Get-AzV2Context | ConvertTo-Json");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("SubscriptionId", output);
        Assert.Contains("TenantId", output);
    }

    [Fact]
    public async Task DisconnectAzV2Account_ShouldSucceed()
    {
        var (exitCode, output) = await RunPowerShellAsync("Connect-AzV2Account; Disconnect-AzV2Account");
        
        Assert.Equal(0, exitCode);
    }

}
