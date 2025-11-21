using System.Diagnostics;
using System.Text;

namespace Core.Agent.Tests;

/// <summary>
/// Tests for LocalAgent (--local-model flag).
/// These tests verify that the agent uses Phi-3.5-mini when the --local-model flag is specified.
/// </summary>
public class LocalAgentTests
{
    /// <summary>
    /// Polling interval in milliseconds for checking process output during model initialization.
    /// </summary>
    private const int PollingIntervalMs = 100;

    [Fact(Skip = "Disabled to avoid file locking conflicts with parallel test execution")]
    public async Task CreateAsync_ShouldCreateLocalAgent()
    {
        // Create a temporary prompt file for testing
        var tempPromptPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempPromptPath, "You are a helpful assistant. Answer questions concisely.");
            
            // Updated signature: CreateAsync(string flavor, string? promptFilePath)
            var agent = await LocalAgent.CreateAsync("azure cli", tempPromptPath);
            Assert.NotNull(agent);
            
            agent.Dispose();
        }
        finally
        {
            if (File.Exists(tempPromptPath))
            {
                File.Delete(tempPromptPath);
            }
        }
    }

    [Fact]
    public async Task CreateAsync_ShouldDownloadOrUseCachedModel()
    {
        var modelManager = new LlamaModelManager();
        
        // This will either download the model or use cached version
        var modelPath = await modelManager.EnsureModelAsync();
        
        Assert.NotNull(modelPath);
        Assert.True(File.Exists(modelPath), "Model file should exist after EnsureModelAsync");
        Assert.Contains("Phi-3.5-mini", modelPath);
    }

    [Fact]
    public async Task ModelManager_ShouldUsePhi35MiniModel()
    {
        var modelManager = new LlamaModelManager();
        
        // Verify the model manager uses Phi-3.5-mini by checking the cache path
        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache",
            "llama_models");
        
        var expectedModelPath = Path.Combine(cacheDir, "Phi-3.5-mini-instruct-Q4_K_M.gguf");
        
        // The modelPath property should point to Phi-3.5-mini
        var actualModelPath = await modelManager.EnsureModelAsync();
        Assert.Equal(expectedModelPath, actualModelPath);
    }

    [Fact(Skip = "Disabled to avoid file locking conflicts with parallel test execution")]
    public async Task RunStreamingAsync_ShouldReturnResponse_WhenValidInput()
    {
        // Create a temporary prompt file for testing
        var tempPromptPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempPromptPath, "You are a helpful assistant. Answer questions concisely. When asked for a number, provide only the number.");
            
            // Updated signature: CreateAsync(string flavor, string? promptFilePath)
            var agent = await LocalAgent.CreateAsync("azure cli", tempPromptPath);
            var responseBuilder = new StringBuilder();

            // Simple test question
            await foreach (var update in agent.RunStreamingAsync("What is 2+2? Answer with just the number."))
            {
                responseBuilder.Append(update.ToString());
            }

            var response = responseBuilder.ToString();
            Assert.NotEmpty(response);
            
            agent.Dispose();
        }
        finally
        {
            if (File.Exists(tempPromptPath))
            {
                File.Delete(tempPromptPath);
            }
        }
    }

    [Fact(Skip = "Disabled due to broken pipe error when process exits before stdin write")]
    public async Task ProgramExecution_ShouldRunWithLocalModelFlag()
    {
        // Get the Core.Agent executable path
        var agentExePath = GetAgentExecutablePath();
        Assert.True(File.Exists(agentExePath), $"Agent executable not found at: {agentExePath}");

        // Run the agent with --local-model flag (uses LocalAgent)
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = agentExePath,
                Arguments = "--local-model",
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

        // Wait for model initialization message or download progress
        var initializationTask = Task.Run(() =>
        {
            var timeoutTime = DateTime.Now.AddMinutes(5); // 5 minute timeout for download
            while (!process.HasExited && DateTime.Now < timeoutTime)
            {
                var currentOutput = output.ToString();
                if (currentOutput.Contains("Initializing local Phi-3.5-mini model") ||
                    currentOutput.Contains("Downloading Phi-3.5-mini model") ||
                    currentOutput.Contains("Model loaded successfully") ||
                    currentOutput.Contains("Model download complete"))
                {
                    break;
                }
                Task.Delay(PollingIntervalMs).Wait();
            }
        });

        await initializationTask;

        var fullOutput = output.ToString();
        
        // Verify that local model initialization is mentioned
        Assert.True(
            fullOutput.Contains("Initializing local Phi-3.5-mini model") ||
            fullOutput.Contains("Downloading Phi-3.5-mini model"),
            "Output should mention local Phi-3.5-mini model initialization or download");

        // Send empty line to exit
        await process.StandardInput.WriteLineAsync("");
        await process.StandardInput.FlushAsync();

        // Wait for exit with timeout
        var exited = process.WaitForExit(5000);
        if (!exited)
        {
            process.Kill();
        }
    }

    [Fact]
    public async Task ProgramExecution_WithLocalModelFlag_ShouldUsePhi35Mini()
    {
        // Verify model is downloaded/cached
        var modelManager = new LlamaModelManager();
        var modelPath = await modelManager.EnsureModelAsync();
        
        Assert.Contains("Phi-3.5-mini", modelPath);
        Assert.True(File.Exists(modelPath), "Phi-3.5-mini model should be downloaded or cached");
    }

    [Fact]
    public async Task LocalAgent_ShouldDownloadModel_WhenNotCached()
    {
        var modelManager = new LlamaModelManager();
        
        long downloadedBytes = 0;
        long totalBytes = 0;
        bool progressCallbackInvoked = false;

        var modelPath = await modelManager.EnsureModelAsync((downloaded, total) =>
        {
            progressCallbackInvoked = true;
            downloadedBytes = downloaded;
            totalBytes = total;
        });

        Assert.NotNull(modelPath);
        Assert.True(File.Exists(modelPath));
        Assert.Contains("Phi-3.5-mini", modelPath);
        
        // If model was already cached, progress callback won't be invoked
        // If it was downloaded, verify progress was reported
        if (progressCallbackInvoked)
        {
            Assert.True(totalBytes > 0, "Total bytes should be greater than 0 during download");
            Assert.Equal(totalBytes, downloadedBytes);
        }
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
