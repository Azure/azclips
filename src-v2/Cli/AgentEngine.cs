using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Core.Abstractions;

namespace Core.Cli;

/// <summary>
/// Wraps CloudAgent as an ICommandEngine for seamless integration.
/// Handles streaming responses and JSON response parsing.
/// </summary>
public partial class AgentEngine : ICommandEngine
{
    private readonly IOutputAdapter _output;
    private readonly string _flavor;
    private string? _extractedCommand;

    public AgentEngine(IOutputAdapter output, string flavor )
    {
        _output = output;
        _flavor = flavor;
    }

    public string? ExtractedCommand => _extractedCommand;

    private class AgentResponse
    {
        public string Response { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    public async Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken = default)
    {
        try
        {
            // Create the agent with the specified flavor
            var agent = await Core.Agent.CloudAgent.CreateAsync(_flavor);

            // await _output.WriteLineAsync("Starting AI assistant. with flavor: " + _flavor);
            
            // Initial user input from args
            string userInput = string.Join(" ", args);

            // Conversation loop - continues until executable_command is received
            while (!cancellationToken.IsCancellationRequested)
            {
                await _output.WriteLineAsync(""); // Skip a line before spinner
                
                // Start spinner task
                var spinnerCts = new CancellationTokenSource();
                var spinnerTask = Task.Run(async () =>
                {
                    var spinnerFrames = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
                    int i = 0;
                    try
                    {
                        while (!spinnerCts.Token.IsCancellationRequested)
                        {
                            await _output.WriteColoredAsync($"\r{spinnerFrames[i++ % spinnerFrames.Length]} Thinking...", OutputColor.Cyan);
                            await Task.Delay(100, spinnerCts.Token);
                        }
                    }
                    catch (OperationCanceledException) { }
                }, spinnerCts.Token);

                var responseBuilder = new StringBuilder();
                bool isFirstUpdate = true;

                // Stream agent response
                await foreach (var update in agent.RunStreamingAsync(userInput))
                {
                    if (isFirstUpdate)
                    {
                        // Stop spinner on first response
                        spinnerCts.Cancel();
                        try { await spinnerTask; } catch { }
                        await _output.WriteAsync("\r" + new string(' ', 50) + "\r");
                        isFirstUpdate = false;
                    }

                    responseBuilder.Append(update.ToString());
                }

                // Ensure spinner is stopped
                if (isFirstUpdate)
                {
                    spinnerCts.Cancel();
                    try { await spinnerTask; } catch { }
                    await _output.WriteAsync("\r" + new string(' ', 50) + "\r");
                }

                string fullResponse = responseBuilder.ToString();

                // Parse JSON response
                AgentResponse? parsedResponse = null;
                try
                {
                    parsedResponse = JsonSerializer.Deserialize<AgentResponse>(fullResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (JsonException)
                {
                    // If JSON parsing fails, treat as plain text and exit
                    await _output.WriteColoredAsync(fullResponse, OutputColor.Cyan);
                    await _output.WriteLineAsync("");
                    _extractedCommand = null;
                    break;
                }

                if (parsedResponse == null)
                {
                    await _output.WriteLineAsync("Error: Unable to parse agent response.");
                    _extractedCommand = null;
                    break;
                }

                // Handle response based on type
                switch (parsedResponse.Type.ToLowerInvariant())
                {
                    case "executable_command":
                        // Display the command
                        await _output.WriteColoredAsync(parsedResponse.Response, OutputColor.Cyan);
                        await _output.WriteLineAsync("");
                        
                        // Extract and store the command
                        _extractedCommand = parsedResponse.Response;
                        
                        // For PowerShell flavor, output a marker that PowerShell can capture
                        if (_flavor == "azure powershell")
                        {
                            Console.WriteLine($"__PSEXEC__{_extractedCommand}");
                        }
                        
                        // Exit the conversation loop
                        return;

                    case "sample_command":
                        // Display the syntax example
                        await _output.WriteColoredAsync(parsedResponse.Response, OutputColor.Cyan);
                        await _output.WriteLineAsync("");
                        
                        // This is syntax help, exit without extracting command
                        _extractedCommand = null;
                        return;

                    case "exit":
                        // Display goodbye message
                        await _output.WriteColoredAsync(parsedResponse.Response, OutputColor.Cyan);
                        await _output.WriteLineAsync("");
                        
                        // User wants to exit
                        _extractedCommand = null;
                        return;

                    case "confirmation":
                    case "question":
                        // Display confirmation request
                        await _output.WriteColoredAsync(parsedResponse.Response, OutputColor.Cyan);
                        await _output.WriteLineAsync("");
                        
                        // Prompt for user confirmation
                        if (_flavor == "azure powershell") {
                            await _output.WriteAsync("\n>\r");
                        } else {
                            await _output.WriteAsync("\n> ");
                        }

                        userInput = await _output.ReadLineAsync("") ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(userInput))
                        {
                            await _output.WriteLineAsync("No input provided. Exiting.");
                            _extractedCommand = null;
                            return;
                        }
                        
                        // Continue loop with user's confirmation response
                        break;


                    default:
                        await _output.WriteLineAsync($"Warning: Unknown response type '{parsedResponse.Type}'");
                        _extractedCommand = null;
                        return;
                }
            }
        }
        catch (Exception)
        {
            // Agent unavailable or error - silent failure
            _extractedCommand = null;
        }
    }
}
