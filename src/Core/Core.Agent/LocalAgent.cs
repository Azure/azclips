using LLama;
using LLama.Common;
using LLama.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;

namespace Core.Agent;

/// <summary>
/// Local agent using LlamaSharp for inference with built-in IChatClient extension.
/// </summary>
public class LocalAgent : IDisposable
{
    private readonly AIAgent _agent;
    private readonly LLamaWeights _weights;
    private readonly LLamaContext _context;
    private readonly ILLamaExecutor _executor;

    private LocalAgent(AIAgent agent, LLamaWeights weights, LLamaContext context, ILLamaExecutor executor)
    {
        _agent = agent;
        _weights = weights;
        _context = context;
        _executor = executor;
    }

    public static async Task<LocalAgent> CreateAsync(string flavor = "azure cli", string? promptFilePath = null)
    {
        // Load prompt from file (same path resolution as CloudAgent)
        promptFilePath ??= Path.Combine(AppContext.BaseDirectory, "prompt.txt");
        
        // Load and replace {{flavor}} in the prompt
        string promptContent = File.Exists(promptFilePath)
            ? await File.ReadAllTextAsync(promptFilePath)
            : "You are a helpful assistant.";
        promptContent = promptContent.Replace("{{flavor}}", flavor);
        
        // Ensure model is downloaded and get path
        var modelManager = new LlamaModelManager();
        
        // Show progress during download with spinner
        var spinnerCts = new CancellationTokenSource();
        long currentDownloaded = 0;
        long totalSize = 0;
        bool downloadStarted = false;
        string? modelPath = null;
        
        // Start spinner task
        var spinnerTask = Task.Run(async () =>
        {
            string[] spinner = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
            int i = 0;
            
            try
            {
                while (!spinnerCts.Token.IsCancellationRequested)
                {
                    if (downloadStarted && totalSize > 0)
                    {
                        double percentComplete = (double)currentDownloaded / totalSize * 100;
                        double mbDownloaded = currentDownloaded / (1024.0 * 1024.0);
                        double mbTotal = totalSize / (1024.0 * 1024.0);
                        
                        Console.Write($"\r{spinner[i++ % spinner.Length]} {percentComplete:F1}% ({mbDownloaded:F1} MB / {mbTotal:F1} MB)");
                    }
                    await Task.Delay(100, spinnerCts.Token);
                }
            }
            catch (TaskCanceledException)
            {
                // Spinner task cancelled as expected when download completes.
                Console.WriteLine("\rSpinner stopped (download complete or cancelled).");
            }
        }, spinnerCts.Token);
        
        // Download with progress callback
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Downloading Phi-3.5-mini model (~2.4GB)...");
        Console.CursorVisible = false;
        
        modelPath = await modelManager.EnsureModelAsync((downloaded, total) =>
        {
            if (!downloadStarted)
            {
                downloadStarted = true;
            }
            currentDownloaded = downloaded;
            totalSize = total;
        });
        
        // Stop spinner
        spinnerCts.Cancel();
        try
        {
            await spinnerTask;
        }
        catch (TaskCanceledException)
        {
            // Intentionally ignored: TaskCanceledException is expected when cancelling the spinner task.
        }
        Console.Write("\r                                                        \r");
        Console.WriteLine("✓ Model download complete!");
        Console.CursorVisible = true;
        Console.ResetColor();
        
        spinnerCts.Dispose();

        // Load model weights
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Loading model into memory...");
        Console.ResetColor();
        
        try
        {
            var parameters = modelManager.CreateModelParams(modelPath!);
            var weights = await LLamaWeights.LoadFromFileAsync(parameters);
            
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("✓ Model loaded successfully!");
            Console.ResetColor();
            
            // Use the prompt content that was already loaded and had {{flavor}} replaced
            var instructions = promptContent;
            
            // Create context and executor (ILLamaExecutor interface)
            var context = weights.CreateContext(parameters);
            var chatSession = new ChatSession(new InteractiveExecutor(context));
            
            // Use LlamaSharp's built-in IChatClient extension with the initialized executor
            ILLamaExecutor executor = chatSession.Executor;
            IChatClient chatClient = executor.AsChatClient();

            // Create AI agent with instructions (matching CloudAgent pattern)
            var agent = chatClient.CreateAIAgent(instructions: instructions);

            return new LocalAgent(agent, weights, context, executor);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Error loading model: {ex.Message}");
            Console.WriteLine("\nThis may be due to CPU instruction set incompatibility.");
            Console.WriteLine("The model requires AVX2 support. Please check your CPU capabilities.");
            Console.ResetColor();
            throw;
        }
    }

    /// <summary>
    /// Runs the agent with streaming response.
    /// </summary>
    public async IAsyncEnumerable<AgentRunResponseUpdate> RunStreamingAsync(string userInput)
    {
        await foreach (var update in _agent.RunStreamingAsync(userInput))
        {
            yield return update;
        }
    }

    public void Dispose()
    {
        _context?.Dispose();
        _weights?.Dispose();
    }
}


