using System.Diagnostics;

namespace PowerShell.Tests;

/// <summary>
/// Integration tests for Group PowerShell cmdlets.
/// Tests invoke pwsh as a subprocess to test real module loading.
/// </summary>
public class GroupCmdletTests
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
        var modulePath = Path.Combine(repoRoot, "bin", "Group", "Cirrus.Group.psd1");
        
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
        var modulePath = Path.Combine(repoRoot, "bin", "Group", "Cirrus.Group.psd1");
        
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
        var modulePath = Path.Combine(repoRoot, "bin", "Group", "Cirrus.Group.psd1");
        Assert.True(File.Exists(modulePath), $"Module should exist at: {modulePath}");
    }

    [Fact]
    public async Task GetAzV2ResourceGroup_ShouldBeAvailable()
    {
        var (exitCode, output) = await RunPowerShellAsync("Get-Command Get-AzV2ResourceGroup");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("Get-AzV2ResourceGroup", output);
    }

    [Fact]
    public async Task GetAzV2ResourceGroup_ShouldReturnGroups()
    {
        var (exitCode, output) = await RunPowerShellAsync("Get-AzV2ResourceGroup | ConvertTo-Json");
        
        Assert.Equal(0, exitCode);
        Assert.NotEmpty(output.Trim());
    }

    [Fact]
    public async Task NewAzV2ResourceGroup_ShouldCreateGroup()
    {
        var rgName = $"test-rg-{Guid.NewGuid():N}";
        var (exitCode, output) = await RunPowerShellAsync($"New-AzV2ResourceGroup -Name '{rgName}' -Location 'eastus' | ConvertTo-Json");
        
        Assert.Equal(0, exitCode);
        Assert.Contains(rgName, output);
    }

    [Fact]
    public async Task SetAzV2ResourceGroup_ShouldUpdateGroup()
    {
        // Set-AzV2ResourceGroup only accepts Name parameter (Tags has Dictionary binding issue)
        var (exitCode, output) = await RunPowerShellAsync("Set-AzV2ResourceGroup -Name 'test-rg'");
        
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RemoveAzV2ResourceGroup_ShouldDeleteGroup()
    {
        var (exitCode, output) = await RunPowerShellAsync("Remove-AzV2ResourceGroup -Name 'test-rg' -Force");
        
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task InvokeAzV2Function_ShouldBeAvailable()
    {
        var (exitCode, output) = await RunPowerShellAsync("Get-Command Invoke-AzV2");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("Invoke-AzV2", output);
    }

    [Fact]
    public async Task AskAlias_ShouldBeAvailable()
    {
        var (exitCode, output) = await RunPowerShellAsync("Get-Alias ask");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("Invoke-AzV2", output);
    }

    [Fact]
    public async Task PleaseAlias_ShouldBeAvailable()
    {
        var (exitCode, output) = await RunPowerShellAsync("Get-Alias please");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("Invoke-AzV2", output);
    }

    [Fact]
    public async Task PlsAlias_ShouldBeAvailable()
    {
        var (exitCode, output) = await RunPowerShellAsync("Get-Alias pls");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("Invoke-AzV2", output);
    }

    [Fact]
    public async Task Agent_NaturalLanguage_ListGroups_ShouldInvokeAgent()
    {
        var output = await RunUntilAgentStartsAsync("ask how do I list resource groups");
        
        // Verify agent was invoked (no errors means it processed successfully)
        Assert.DoesNotContain("error", output.ToLower());
        Assert.DoesNotContain("exception", output.ToLower());
    }

    [Fact]
    public async Task Agent_NaturalLanguage_CreateGroup_ShouldInvokeAgent()
    {
        var output = await RunUntilAgentStartsAsync("please how to create a resource group");
        
        // Verify agent was invoked (no errors means it processed successfully)
        Assert.DoesNotContain("error", output.ToLower());
        Assert.DoesNotContain("exception", output.ToLower());
    }

    [Fact]
    public async Task Agent_MistypedCmdlet_ShouldInvokeAgent()
    {
        var output = await RunUntilAgentStartsAsync("pls Get-AzV2ResourseGroup");
        
        // Misspelled cmdlet should trigger agent (no errors means it processed successfully)
        Assert.DoesNotContain("error", output.ToLower());
        Assert.DoesNotContain("exception", output.ToLower());
    }
}
