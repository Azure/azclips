using System.Diagnostics;
using System.Text;

namespace Core.Agent.Tests;

/// <summary>
/// Integration tests for Core.Agent Program execution.
/// These tests verify the end-to-end execution of the agent program with different command-line arguments.
/// </summary>
public class ProgramIntegrationTests
{
    private const int InitializationWaitDelayMs = 2000;
    private const int ResponseWaitDelayMs = 1000;
    [Fact]
    public async Task Program_ShouldExist_AndBeExecutable()
    {
        var agentExePath = GetAgentExecutablePath();
        Assert.True(File.Exists(agentExePath), $"Agent executable not found at: {agentExePath}");
    }

    [Fact]
    public async Task Program_WithoutFlags_ShouldUseCloudAgent()
    {
        var hasAzureConfig = CheckAzureConfiguration();
        
        if (!hasAzureConfig)
        {
            // Skip test if .env not configured
            return;
        }

        var output = await RunAgentWithInput("", "What is 1+1?");
        
        Assert.DoesNotContain("Initializing local Phi-3.5-mini model", output);
        Assert.DoesNotContain("Downloading Phi-3.5-mini model", output);
    }

    [Fact]
    public async Task Program_WithLocalModelFlag_ShouldUseLocalAgent()
    {
        var output = await RunAgentWithInput("--local-model", "");
        
        Assert.True(
            output.Contains("Initializing local Phi-3.5-mini model") ||
            output.Contains("Downloading Phi-3.5-mini model") ||
            output.Contains("Loading model into memory"),
            "Output should indicate local Phi-3.5-mini model usage");
    }

    [Fact]
    public async Task Program_WithLocalModelFlag_ShouldDownloadPhi35Mini()
    {
        // Verify the model file is present after running with --local-model
        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache",
            "llama_models");
        
        var modelPath = Path.Combine(cacheDir, "Phi-3.5-mini-instruct-Q4_K_M.gguf");

        // Run the agent to ensure model is downloaded
        _ = await RunAgentWithInput("--local-model", "");

        Assert.True(File.Exists(modelPath), $"Phi-3.5-mini model should exist at: {modelPath}");
        
        // Verify file size is reasonable (model should be around 2.4GB)
        var fileInfo = new FileInfo(modelPath);
        Assert.True(fileInfo.Length > 1_000_000_000, "Model file should be larger than 1GB");
        Assert.True(fileInfo.Length < 5_000_000_000, "Model file should be smaller than 5GB");
    }

    [Fact]
    public async Task ModelManager_ShouldUseCorrectModelUrl()
    {
        var modelManager = new LlamaModelManager();
        
        // Create a test to ensure we're using the Phi-3.5-mini model
        var modelPath = await modelManager.EnsureModelAsync();
        
        Assert.Contains("Phi-3.5-mini-instruct-Q4_K_M.gguf", modelPath);
    }

    [Fact]
    public void ModelManager_ShouldCacheInUserProfile()
    {
        var expectedCacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache",
            "llama_models");

        Assert.True(Directory.Exists(expectedCacheDir), 
            "Cache directory should exist or be creatable in user profile");
    }

    private static async Task<string> RunAgentWithInput(string arguments, string input)
    {
        var agentExePath = GetAgentExecutablePath();
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = agentExePath,
                Arguments = arguments,
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

        // Wait a bit for initialization
        await Task.Delay(InitializationWaitDelayMs);

        // Send input if provided
        if (!string.IsNullOrEmpty(input))
        {
            await process.StandardInput.WriteLineAsync(input);
            await process.StandardInput.FlushAsync();
            await Task.Delay(ResponseWaitDelayMs);
        }

        // Send empty line to exit
        await process.StandardInput.WriteLineAsync("");
        await process.StandardInput.FlushAsync();

        // Wait for exit with timeout
        var exited = process.WaitForExit(5000);
        if (!exited)
        {
            process.Kill();
        }

        return output.ToString() + errorOutput.ToString();
    }

    private static bool CheckAzureConfiguration()
    {
        try
        {
            DotNetEnv.Env.Load(Path.Combine(GetRepositoryRoot(), ".env"));
        }
        catch
        {
            // .env file not found or couldn't be loaded
        }

        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")) &&
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT"));
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
