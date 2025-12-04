using System.Text;
using System.Text.RegularExpressions;
using Core.Abstractions;

namespace Core.Cli;

/// <summary>
/// Wraps CloudAgent as an ICommandEngine for seamless integration.
/// Handles streaming responses, spinner animation, and [DONE] marker detection.
/// </summary>
public partial class AgentEngine : ICommandEngine
{
    private readonly IOutputAdapter _output;
    private readonly string _flavor;
    private string? _extractedCommand;

    public AgentEngine(IOutputAdapter output, string flavor = "azure cli")
    {
        _output = output;
        _flavor = flavor;
    }

    public string? ExtractedCommand => _extractedCommand;

    [GeneratedRegex(@"```(?:bash|sh|powershell|pwsh)?\s*(.*?)\s*```", RegexOptions.Singleline)]
    private static partial Regex CodeBlockRegex();

    public async Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken = default)
    {
        try
        {
            // Create the agent with the specified flavor
            var agent = await Core.Agent.CloudAgent.CreateAsync(_flavor);

            // Initial user input from args
            string userInput = string.Join(" ", args);
            string? potentialCommand = null;

            // Conversation loop - continues until [DONE] marker
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

                    var text = update.ToString();
                    responseBuilder.Append(text);
                    
                    // Filter out [DONE] marker from display
                    if (!text.Contains("[DONE]", StringComparison.OrdinalIgnoreCase))
                    {
                        await _output.WriteColoredAsync(text, OutputColor.Cyan);
                    }

                }

                // Ensure spinner is stopped
                if (isFirstUpdate)
                {
                    spinnerCts.Cancel();
                    try { await spinnerTask; } catch { }
                    await _output.WriteAsync("\r" + new string(' ', 50) + "\r");
                }

                await _output.WriteLineAsync("");

                string fullResponse = responseBuilder.ToString();

                // Extract command from response (look for command lines)
                if (potentialCommand == null)
                {
                    // Split into lines and find command lines
                    var lines = fullResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        
                        // For PowerShell flavor, look for AzV2 cmdlets
                        if (_flavor == "azure powershell")
                        {
                            if (trimmed.StartsWith("New-AzV2") || trimmed.StartsWith("Get-AzV2") || 
                                trimmed.StartsWith("Set-AzV2") || trimmed.StartsWith("Remove-AzV2"))
                            {
                                // Check if command has placeholders
                                if (!trimmed.Contains('<') && !trimmed.Contains('>'))
                                {
                                    potentialCommand = trimmed;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // For Azure CLI flavor, look for az commands
                            if (trimmed.StartsWith("az ", StringComparison.OrdinalIgnoreCase))
                            {
                                // Remove 'az ' prefix
                                var command = trimmed.Substring(3).Trim();
                                
                                // Check if command has placeholders
                                if (!command.Contains('<') && !command.Contains('>'))
                                {
                                    potentialCommand = command;
                                    break;
                                }
                            }
                        }
                    }
                }

                // Check if this is a help/syntax response (contains placeholders)
                if (fullResponse.Contains('<') && fullResponse.Contains('>'))
                {
                    // This is a "how to" response, exit without prompting
                    _extractedCommand = null;
                    break;
                }

                // Check if conversation is done
                if (fullResponse.Contains("[DONE]", StringComparison.OrdinalIgnoreCase))
                {
                    // Check if execution was cancelled
                    string responseLower = fullResponse.ToLowerInvariant();
                    if (responseLower.Contains("cancelled") || 
                        responseLower.Contains("won't execute") ||
                        responseLower.Contains("not executing"))
                    {
                        _extractedCommand = null;
                    }
                    else if (!string.IsNullOrEmpty(potentialCommand))
                    {
                        _extractedCommand = potentialCommand;
                        
                        // For PowerShell flavor, output a marker that PowerShell can capture
                        if (_flavor == "azure powershell")
                        {
                            // Output to stdout so PowerShell can capture it
                            Console.WriteLine($"__PSEXEC__{potentialCommand}");
                        }
                    }
                    else
                    {
                        _extractedCommand = null;
                    }
                    break;
                }

                // Get next user input - output prompt with carriage return
                await _output.WriteAsync("\n>\r");
                userInput = await _output.ReadLineAsync("") ?? string.Empty;

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
        }
        catch (Exception)
        {
            // Agent unavailable or error - silent failure
            _extractedCommand = null;
        }
    }
}
