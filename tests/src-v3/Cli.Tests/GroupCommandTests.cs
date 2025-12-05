using System.Diagnostics;

namespace Cli.Tests;

/// <summary>
/// Integration tests for azv3 CLI commands.
/// Tests both structured commands and agent natural language fallback.
/// </summary>
public class GroupCommandTests
{
    private static string GetAzV3Path()
    {
        var repoRoot = GetRepositoryRoot();
        var azv3Path = Path.Combine(repoRoot, "bin", "azv3");
        
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            azv3Path += ".exe";
        }
        
        return azv3Path;
    }

    private static string GetRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, ".git", "config")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? AppContext.BaseDirectory;
    }

    private static async Task<(int exitCode, string output)> RunAzV3Async(string args, int timeoutSeconds = 10)
    {
        var azv3Path = GetAzV3Path();
        
        var startInfo = new ProcessStartInfo
        {
            FileName = azv3Path,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        // Disable interactive prompts for testing
        startInfo.Environment["CI"] = "true";
        startInfo.Environment["AZURECLI_NONINTERACTIVE"] = "1";

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
            throw new TimeoutException($"Command '{args}' timed out after {timeoutSeconds} seconds");
        }

        var output = outputBuilder.ToString() + errorBuilder.ToString();
        return (process.ExitCode, output);
    }

    private static async Task<string> RunUntilAgentStartsAsync(string args, int timeoutSeconds = 5)
    {
        var azv3Path = GetAzV3Path();
        
        var startInfo = new ProcessStartInfo
        {
            FileName = azv3Path,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
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
    public void AzV3_ShouldExist()
    {
        var azv3Path = GetAzV3Path();
        Assert.True(File.Exists(azv3Path), $"azv3 executable should exist at: {azv3Path}");
    }

    [Fact]
    public async Task GroupList_ShouldSucceed()
    {
        var (exitCode, output) = await RunAzV3Async("group list");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("resource groups", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GroupShow_ShouldSucceed()
    {
        var (exitCode, output) = await RunAzV3Async("group show --name test-rg --location eastus");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("test-rg", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GroupCreate_ShouldSucceed()
    {
        var rgName = $"test-rg-{Guid.NewGuid():N}";
        var (exitCode, output) = await RunAzV3Async($"group create --name {rgName} --location eastus");
        
        Assert.Equal(0, exitCode);
        Assert.Contains(rgName, output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GroupDelete_ShouldSucceed()
    {
        var (exitCode, output) = await RunAzV3Async("group delete --name test-rg");
        
        Assert.Equal(0, exitCode);
        Assert.Contains("delete", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Agent_NaturalLanguage_ListGroups_ShouldInvokeAgent()
    {
        var output = await RunUntilAgentStartsAsync("how do I list resource groups");
        
        // Verify agent was invoked by checking for "Thinking" indicator
        Assert.Contains("Thinking", output);
    }

    [Fact]
    public async Task Agent_NaturalLanguage_CreateGroup_ShouldInvokeAgent()
    {
        var output = await RunUntilAgentStartsAsync("how to create a resource group");
        
        // Verify agent was invoked
        Assert.Contains("Thinking", output);
    }

    [Fact]
    public async Task Agent_MistypedCommand_ShouldInvokeAgent()
    {
        var output = await RunUntilAgentStartsAsync("grup list");
        
        // Misspelled commands should trigger agent
        Assert.Contains("Thinking", output);
    }
}
