using LLama.Common;
using LLama.Native;

namespace Core.Agent;

/// <summary>
/// Manages LLamaSharp model lifecycle including downloading, caching, and loading models.
/// </summary>
public class LlamaModelManager
{
    private const string DefaultModelUrl = "https://huggingface.co/bartowski/Phi-3.5-mini-instruct-GGUF/resolve/main/Phi-3.5-mini-instruct-Q4_K_M.gguf";
    private const string DefaultModelFileName = "Phi-3.5-mini-instruct-Q4_K_M.gguf";
    
    private readonly string _modelDirectory;
    private readonly string _modelPath;

    public LlamaModelManager(string? modelDirectory = null)
    {
        _modelDirectory = modelDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache",
            "llama_models");
        
        _modelPath = Path.Combine(_modelDirectory, DefaultModelFileName);
        Directory.CreateDirectory(_modelDirectory);
        
        // Suppress verbose llama.cpp logging - only show errors
        // Only configure if not already loaded (important for testing)
        try
        {
            NativeLibraryConfig.All.WithLogCallback((level, message) =>
            {
                if (level == LLamaLogLevel.Error)
                {
                    Console.Error.WriteLine($"[LLama Error] {message}");
                }
            });
        }
        catch (InvalidOperationException)
        {
            // Library already loaded, configuration already set
            // This is expected in test scenarios where multiple tests run
        }
    }

    public async Task<string> EnsureModelAsync(Action<long, long>? progressCallback = null)
    {
        if (File.Exists(_modelPath))
        {
            return _modelPath;
        }

        await DownloadModelAsync(DefaultModelUrl, _modelPath, progressCallback);
        return _modelPath;
    }

    public ModelParams CreateModelParams(string modelPath)
    {
        var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;
        bool isArm = arch == System.Runtime.InteropServices.Architecture.Arm64;
        
        return new ModelParams(modelPath)
        {
            ContextSize = 2048,
            // On ARM64 (Apple Silicon), use Metal GPU acceleration
            // On x86-64, use CPU-only (or CUDA if available)
            GpuLayerCount = isArm ? 32 : 0
        };
    }

    private async Task DownloadModelAsync(string url, string destinationPath, Action<long, long>? progressCallback)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromHours(1) };
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;
        var tempPath = destinationPath + ".tmp";
        
        try
        {
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);
            
            var buffer = new byte[8192];
            long totalBytesRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalBytesRead += bytesRead;

                if (totalBytes.HasValue && progressCallback != null)
                {
                    progressCallback(totalBytesRead, totalBytes.Value);
                }
            }

            File.Move(tempPath, destinationPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }
    }
}
