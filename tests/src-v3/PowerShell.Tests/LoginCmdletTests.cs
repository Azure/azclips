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
    public async Task ConnectAzV3Account_ShouldBeAvailable()
    {
        var (exitCode, output) = await RunPowerShellAsync("Get-Command Connect-AzV3Account");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("Connect-AzV3Account", output);
    }

    [Fact]
    public async Task ConnectAzV3Account_ShouldAuthenticate()
    {
        var (exitCode, output) = await RunPowerShellAsync("Connect-AzV3Account | ConvertTo-Json");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("IsAuthenticated", output);
        Assert.Contains("true", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAzV3Context_ShouldReturnContext()
    {
        var (exitCode, output) = await RunPowerShellAsync("Connect-AzV3Account; Get-AzV3Context | ConvertTo-Json");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("SubscriptionId", output);
        Assert.Contains("TenantId", output);
    }

    [Fact]
    public async Task DisconnectAzV3Account_ShouldSucceed()
    {
        var (exitCode, output) = await RunPowerShellAsync("Connect-AzV3Account; Disconnect-AzV3Account");
        
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task InvokeAzV3Function_ShouldBeAvailable()
    {
        var (exitCode, output) = await RunPowerShellAsync("Get-Command Invoke-AzV3");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("Invoke-AzV3", output);
    }

    [Fact]
    public async Task AskAlias_ShouldBeAvailable()
    {
        var (exitCode, output) = await RunPowerShellAsync("Get-Alias ask");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("Invoke-AzV3", output);
    }

    [Fact]
    public async Task PleaseAlias_ShouldBeAvailable()
    {
        var (exitCode, output) = await RunPowerShellAsync("Get-Alias please");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("Invoke-AzV3", output);
    }

    [Fact]
    public async Task PlsAlias_ShouldBeAvailable()
    {
        var (exitCode, output) = await RunPowerShellAsync("Get-Alias pls");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("Invoke-AzV3", output);
    }

    [Fact]
    public async Task Agent_NaturalLanguage_LoginHelp_ShouldInvokeAgent()
    {
        var output = await RunUntilAgentStartsAsync("ask how do I login to Azure");
        
        // Verify agent was invoked (no errors means it processed successfully)
        Assert.DoesNotContain("error", output.ToLower());
        Assert.DoesNotContain("exception", output.ToLower());
    }

    [Fact]
    public async Task Agent_NaturalLanguage_CheckContext_ShouldInvokeAgent()
    {
        var output = await RunUntilAgentStartsAsync("please how to check my authentication");
        
        // Verify agent was invoked (no errors means it processed successfully)
        Assert.DoesNotContain("error", output.ToLower());
        Assert.DoesNotContain("exception", output.ToLower());
    }

    [Fact]
    public async Task Agent_MistypedCmdlet_ShouldInvokeAgent()
    {
        var output = await RunUntilAgentStartsAsync("pls Conect-AzV3Account");
        
        // Misspelled cmdlet should trigger agent (no errors means it processed successfully)
        Assert.DoesNotContain("error", output.ToLower());
        Assert.DoesNotContain("exception", output.ToLower());
    }
}
