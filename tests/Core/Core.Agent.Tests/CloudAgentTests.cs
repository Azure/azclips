using System.Diagnostics;
using System.Text;
using DotNetEnv;

namespace Core.Agent.Tests;

/// <summary>
/// Tests for CloudAgent (no --local-model flag).
/// These tests require .env file with Azure OpenAI credentials and will be skipped if not present.
/// </summary>
public class CloudAgentTests
{
    private const int PollingIntervalMs = 100;
    private readonly bool _hasAzureConfig;

    public CloudAgentTests()
    {
        // Try to load .env from repository root
        var repoRoot = GetRepositoryRoot();
        var envPath = Path.Combine(repoRoot, ".env");
        
        if (File.Exists(envPath))
        {
            Env.Load(envPath);
        }

        // Check if Azure OpenAI configuration is available
        _hasAzureConfig = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")) &&
                         !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")) &&
                         !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT"));
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateCloudAgent_WhenAzureConfigPresent()
    {
        if (!_hasAzureConfig)
        {
            // Skip test if .env not configured (e.g., in CI/CD)
            return;
        }

        var agent = await CloudAgent.CreateAsync();
        Assert.NotNull(agent);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenAzureConfigMissing()
    {
        // Temporarily clear environment variables
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        var deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");

        try
        {
            Environment.SetEnvironmentVariable("AZURE_OPENAI_ENDPOINT", null);
            Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", null);
            Environment.SetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT", null);

            await Assert.ThrowsAsync<InvalidOperationException>(async () => 
                await CloudAgent.CreateAsync());
        }
        finally
        {
            // Restore environment variables
            Environment.SetEnvironmentVariable("AZURE_OPENAI_ENDPOINT", endpoint);
            Environment.SetEnvironmentVariable("AZURE_OPENAI_API_KEY", apiKey);
            Environment.SetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT", deployment);
        }
    }

    [Fact]
    public async Task RunStreamingAsync_ShouldReturnResponse_WhenValidInput()
    {
        if (!_hasAzureConfig)
        {
            // Skip test if .env not configured
            return;
        }

        var agent = await CloudAgent.CreateAsync();
        var responseBuilder = new StringBuilder();

        await foreach (var update in agent.RunStreamingAsync("What is 2+2? Answer with just the number."))
        {
            responseBuilder.Append(update.ToString());
        }

        var response = responseBuilder.ToString();
        Assert.NotEmpty(response);
        Assert.Contains("4", response);
    }

    [Fact]
    public async Task ProgramExecution_ShouldRunWithoutLocalModelFlag()
    {
        if (!_hasAzureConfig)
        {
            // Skip test if .env not configured
            return;
        }

        // Get the Core.Agent executable path
        var agentExePath = GetAgentExecutablePath();
        Assert.True(File.Exists(agentExePath), $"Agent executable not found at: {agentExePath}");

        // Run the agent without --local-model flag (uses CloudAgent)
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = agentExePath,
                Arguments = "",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var output = new StringBuilder();
        var errorOutput = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null) output.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null) errorOutput.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Send a simple question
        await process.StandardInput.WriteLineAsync("What is 2+2? Answer with just the number.");
        await process.StandardInput.FlushAsync();
        
        // Wait for response with timeout
        var completionTask = Task.Run(() =>
        {
            while (!process.HasExited && !output.ToString().Contains("4"))
            {
                Task.Delay(PollingIntervalMs).Wait();
            }
        });

        var timeout = Task.Delay(30000); // 30 second timeout
        await Task.WhenAny(completionTask, timeout);

        // Send empty line to exit
        await process.StandardInput.WriteLineAsync("");
        await process.StandardInput.FlushAsync();

        // Wait for exit with timeout
        var exited = process.WaitForExit(5000);
        if (!exited)
        {
            process.Kill();
        }

        var fullOutput = output.ToString();
        Assert.DoesNotContain("Initializing local Phi-3.5-mini model", fullOutput);
        Assert.DoesNotContain("Downloading Phi-3.5-mini model", fullOutput);
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

    private static string GetAgentExecutablePath()
    {
        var repoRoot = GetRepositoryRoot();
        var agentPath = Path.Combine(repoRoot, "src", "Core", "Core.Agent", "bin", "Debug", "net10.0", "Core.Agent");
        
        // On Windows, add .exe extension
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            agentPath += ".exe";
        }

        return agentPath;
    }
}
