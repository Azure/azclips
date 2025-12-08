using System.Diagnostics;

namespace Cli.Tests;

/// <summary>
/// Integration tests for azv2 login commands.
/// </summary>
public class LoginCommandTests
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

    private static async Task<(int exitCode, string output)> RunAzV2Async(string args, int timeoutSeconds = 10)
    {
        var repoRoot = GetRepositoryRoot();
        var azPath = Path.Combine(repoRoot, "bin", "azv2");
        
        var startInfo = new ProcessStartInfo
        {
            FileName = azPath,
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
        var repoRoot = GetRepositoryRoot();
        var azPath = Path.Combine(repoRoot, "bin", "azv2");
        
        var startInfo = new ProcessStartInfo
        {
            FileName = azPath,
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
    public async Task Login_ShouldSucceed()
    {
        var (exitCode, output) = await RunAzV2Async("login");
        
        Assert.Equal(0, exitCode);
        Assert.True(
            output.Contains("authenticated", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("successful", StringComparison.OrdinalIgnoreCase),
            $"Output should contain authentication success. Actual: {output}");
    }

    [Fact]
    public async Task Agent_NaturalLanguage_LoginHelp_ShouldInvokeAgent()
    {
        var output = await RunUntilAgentStartsAsync("how do I login to Azure");
        
        // Verify agent was invoked
        Assert.Contains("Thinking", output);
    }

    [Fact]
    public async Task Agent_NaturalLanguage_ShowContextHelp_ShouldInvokeAgent()
    {
        var output = await RunUntilAgentStartsAsync("how to check my current authentication");
        
        // Verify agent was invoked
        Assert.Contains("Thinking", output);
    }

    [Fact]
    public async Task Agent_MistypedCommand_LoginTypo_ShouldInvokeAgent()
    {
        var output = await RunUntilAgentStartsAsync("logn");
        
        // Misspelled commands should trigger agent
        Assert.Contains("Thinking", output);
    }
}
