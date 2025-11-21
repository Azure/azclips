using Core.Abstractions;
using System.Text;
using System.Text.RegularExpressions;

namespace Core.Agent;

/// <summary>
/// An implementation of ICommandEngine that manages interactive agent conversations.
/// Supports multiple agent types (Cloud, Local, Azure Copilot) and handles the complete
/// conversation lifecycle including initialization, streaming responses, and conversation loops.
/// Can extract executable commands and pass control back to DefaultCommandEngine.
/// </summary>
public class AgentEngine : ICommandEngine
{
    private readonly ITelemetryService _telemetry;
    private readonly TextWriter _output;
    private readonly TextReader _input;
    
    /// <summary>
    /// Gets the extracted command if the user confirmed execution, otherwise null.
    /// This allows the caller to hand back to DefaultCommandEngine.
    /// </summary>
    public string? ExtractedCommand { get; private set; }

    /// <summary>
    /// Initializes a new instance of the AgentEngine.
    /// </summary>
    /// <param name="telemetry">Telemetry service for logging.</param>
    /// <param name="output">Output writer for displaying responses (defaults to Console.Out).</param>
    /// <param name="input">Input reader for user input (defaults to Console.In).</param>
    public AgentEngine(
        ITelemetryService telemetry,
        TextWriter? output = null,
        TextReader? input = null)
    {
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _output = output ?? Console.Out;
        _input = input ?? Console.In;
    }

    /// <summary>
    /// Executes the agent command by parsing arguments, initializing the appropriate agent,
    /// and managing the interactive conversation loop.
    /// </summary>
    /// <param name="args">Command-line arguments for agent configuration and initial query.</param>
    /// <param name="cancellationToken">Cancellation token to stop execution.</param>
    public async Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            _telemetry.Log("AgentEngine: Starting agent execution");

            // Parse command-line arguments
            bool useCopilot = args.Contains("--use-copilot");
            bool useLocalModel = args.Contains("--local-model");
            string flavor = "azure cli"; // Default flavor
            
            // Parse --flavor argument
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--flavor" && i + 1 < args.Length)
                {
                    flavor = args[i + 1].ToLowerInvariant();
                    break;
                }
            }
            
            string? initialQuery = ExtractInitialQuery(args);

            // Create the agent based on the mode (priority: --use-copilot > --local-model > cloud)
            object agent = await InitializeAgentAsync(useCopilot, useLocalModel, flavor, cancellationToken);

            // Get initial user input
            string? userInput = await GetInitialInputAsync(initialQuery);

            if (string.IsNullOrWhiteSpace(userInput))
            {
                await _output.WriteLineAsync("No question provided.");
                return;
            }

            // Run conversation loop
            await RunConversationLoopAsync(agent, userInput, cancellationToken);

            _telemetry.Log("AgentEngine: Agent execution completed successfully");
        }
        catch (OperationCanceledException)
        {
            _telemetry.Log("AgentEngine: Operation cancelled");
            await _output.WriteLineAsync("\nOperation cancelled.");
        }
        catch (Exception ex)
        {
            _telemetry.Log($"AgentEngine: Error during execution - {ex.Message}");
            Console.ForegroundColor = ConsoleColor.Red;
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            Console.ResetColor();
            throw;
        }
    }

    /// <summary>
    /// Extracts the initial query from command-line arguments.
    /// All arguments that are not flags (don't start with --) are treated as the query.
    /// </summary>
    private string? ExtractInitialQuery(string[] args)
    {
        var queryParts = args.Where(arg => !arg.StartsWith("--")).ToList();
        return queryParts.Any() ? string.Join(" ", queryParts) : null;
    }

    /// <summary>
    /// Initializes the appropriate agent based on command-line flags.
    /// </summary>
    private async Task<object> InitializeAgentAsync(bool useCopilot, bool useLocalModel, string flavor, CancellationToken cancellationToken)
    {
        if (useCopilot)
        {
            _telemetry.Log($"AgentEngine: Initializing Azure Copilot agent with {flavor} flavor");
            await _output.WriteLineAsync($"Initializing Azure Copilot agent with {flavor} flavor...");
            return await AzureCopilotAgent.CreateAsync(flavor);
        }
        else if (useLocalModel)
        {
            _telemetry.Log($"AgentEngine: Initializing local Phi-3.5-mini model with {flavor} flavor");
            await _output.WriteLineAsync($"Initializing local Phi-3.5-mini model with {flavor} flavor...");
            return await LocalAgent.CreateAsync(flavor);
        }
        else
        {
            _telemetry.Log($"AgentEngine: Initializing cloud agent with {flavor} flavor");
            return await CloudAgent.CreateAsync(flavor);
        }
    }

    /// <summary>
    /// Gets the initial user input, either from provided query or by prompting the user.
    /// </summary>
    private async Task<string?> GetInitialInputAsync(string? initialQuery)
    {
        if (!string.IsNullOrWhiteSpace(initialQuery))
        {
            _telemetry.Log($"AgentEngine: Using provided query: {initialQuery}");
            return initialQuery;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        await _output.WriteAsync("\nHow can I help you? ");
        Console.ResetColor();
        return await _input.ReadLineAsync();
    }

    /// <summary>
    /// Runs the interactive conversation loop, streaming responses and handling user input.
    /// Extracts executable commands and tracks user confirmation.
    /// </summary>
    private async Task RunConversationLoopAsync(object agent, string userInput, CancellationToken cancellationToken)
    {
        string? potentialCommand = null;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            await _output.WriteLineAsync();

            // Show spinner while waiting for first response
            var spinnerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var spinnerTask = ShowSpinnerAsync(spinnerCts.Token);

            try
            {
                // Stream the response
                var responseBuilder = new StringBuilder();
                bool firstUpdate = true;

                var streamingTask = agent switch
                {
                    CloudAgent cloudAgent => cloudAgent.RunStreamingAsync(userInput),
                    LocalAgent localAgent => localAgent.RunStreamingAsync(userInput),
                    AzureCopilotAgent copilotAgent => copilotAgent.RunStreamingAsync(userInput),
                    _ => throw new InvalidOperationException("Unknown agent type")
                };

                await foreach (var update in streamingTask.WithCancellation(cancellationToken))
                {
                    // Stop spinner on first update
                    if (firstUpdate)
                    {
                        await StopSpinnerAsync(spinnerCts, spinnerTask);
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        firstUpdate = false;
                    }

                    string updateText = update.ToString();
                    await _output.WriteAsync(updateText);
                    responseBuilder.Append(updateText);
                }

                // Ensure spinner is stopped
                if (firstUpdate)
                {
                    await StopSpinnerAsync(spinnerCts, spinnerTask);
                }

                Console.ResetColor();
                await _output.WriteLineAsync();
                string fullResponse = responseBuilder.ToString();

                // Extract command if present (between ``` markers) and not already extracted
                if (potentialCommand == null)
                {
                    var match = Regex.Match(fullResponse, @"```\s*\n(.*?)\n```", RegexOptions.Singleline);
                    if (match.Success)
                    {
                        string cmd = match.Groups[1].Value.Trim();
                        
                        // Check if command contains placeholders (angle brackets)
                        bool hasPlaceholders = Regex.IsMatch(cmd, @"<[^>]+>");
                        
                        if (!hasPlaceholders)
                        {
                            // This is a fully executable command - save it
                            potentialCommand = cmd;
                            _telemetry.Log($"AgentEngine: Extracted executable command: {potentialCommand}");
                        }
                    }
                }

                // Check if the agent is done
                if (fullResponse.Contains("[DONE]"))
                {
                    _telemetry.Log("AgentEngine: Conversation completed (DONE marker detected)");
                    
                    // Check if agent confirmed or cancelled execution
                    string responseLower = fullResponse.ToLowerInvariant();
                    
                    if (responseLower.Contains("cancelled") || 
                        responseLower.Contains("won't execute") ||
                        responseLower.Contains("not executing"))
                    {
                        // User declined execution
                        _telemetry.Log("AgentEngine: User declined command execution");
                        ExtractedCommand = null;
                    }
                    else if (!string.IsNullOrEmpty(potentialCommand))
                    {
                        // Command was confirmed for execution
                        _telemetry.Log($"AgentEngine: Command confirmed for execution: {potentialCommand}");
                        ExtractedCommand = potentialCommand;
                    }
                    else
                    {
                        // No executable command (e.g., syntax help with placeholders)
                        _telemetry.Log("AgentEngine: No executable command to return");
                        ExtractedCommand = null;
                    }
                    
                    break;
                }

                // Get next user input
                Console.ForegroundColor = ConsoleColor.White;
                await _output.WriteAsync("\n> ");
                Console.ResetColor();
                userInput = await _input.ReadLineAsync() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(userInput))
                {
                    await _output.WriteLineAsync("No input provided. Exiting.");
                    break;
                }

                // Check for exit commands
                if (userInput.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                    userInput.Trim().Equals("quit", StringComparison.OrdinalIgnoreCase))
                {
                    await _output.WriteLineAsync("Goodbye!");
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _telemetry.Log($"AgentEngine: Error in conversation loop - {ex.Message}");
                Console.ForegroundColor = ConsoleColor.Red;
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                Console.ResetColor();
                break;
            }
        }

        Console.ResetColor();
    }

    /// <summary>
    /// Displays an animated spinner while waiting for agent response.
    /// </summary>
    private async Task ShowSpinnerAsync(CancellationToken cancellationToken)
    {
        string[] spinner = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        int i = 0;
        Console.ForegroundColor = ConsoleColor.Cyan;
        
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _output.WriteAsync($"\r{spinner[i++ % spinner.Length]} Thinking...");
                await Task.Delay(100, cancellationToken);
            }
        }
        catch (TaskCanceledException)
        {
            // Expected when cancelled
        }
    }

    /// <summary>
    /// Stops the spinner animation and clears the spinner line.
    /// </summary>
    private async Task StopSpinnerAsync(CancellationTokenSource spinnerCts, Task spinnerTask)
    {
        spinnerCts.Cancel();
        try
        {
            await spinnerTask;
        }
        catch (TaskCanceledException)
        {
            // Expected
        }
        await _output.WriteAsync("\r                    \r"); // Clear spinner line
        spinnerCts.Dispose();
    }
}
