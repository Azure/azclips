using Core.Agent;
using System.Text.RegularExpressions;

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

// Validate flavor
if (flavor != "azure cli" && flavor != "azure powershell")
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Invalid flavor: {flavor}. Must be 'azure cli' or 'azure powershell'.");
    Console.ResetColor();
    return;
}

// Create the agent based on the mode (priority: --use-copilot > --local-model > cloud)
object agent;
if (useCopilot)
{
    Console.WriteLine($"Initializing Azure Copilot agent with {flavor} flavor...");
    agent = await AzureCopilotAgent.CreateAsync(flavor);
}
else if (useLocalModel)
{
    Console.WriteLine($"Initializing local Phi-3.5-mini model with {flavor} flavor...");
    agent = await LocalAgent.CreateAsync(flavor);
}
else
{
    agent = await CloudAgent.CreateAsync(flavor);
}

// Conversation loop
Console.ForegroundColor = ConsoleColor.Cyan;
Console.Write("\nHow can I help you? ");
Console.ResetColor();
string? userInput = Console.ReadLine();

if (string.IsNullOrWhiteSpace(userInput))
{
    Console.WriteLine("No question provided.");
    return;
}

// Keep conversing until [DONE] marker is found
string extractedCommand = string.Empty;

while (true)
{
    try
    {
        Console.WriteLine();
        
        // Show spinner while waiting for first response
        var spinnerCts = new CancellationTokenSource();
        var spinnerTask = Task.Run(async () =>
        {
            string[] spinner = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
            int i = 0;
            Console.ForegroundColor = ConsoleColor.Cyan;
            try
            {
                while (!spinnerCts.Token.IsCancellationRequested)
                {
                    Console.Write($"\r{spinner[i++ % spinner.Length]} Thinking...");
                    await Task.Delay(100, spinnerCts.Token);
                }
            }
            catch (TaskCanceledException)
            {
                // TaskCanceledException is expected when the spinner is stopped (spinnerCts.Cancel()).
                // No action needed.
            }
        }, spinnerCts.Token);
        
        // Stream the response (conversation history is managed inside CloudAgent/LocalAgent)
        var responseBuilder = new System.Text.StringBuilder();
        bool firstUpdate = true;
        
        var streamingTask = agent switch
        {
            CloudAgent cloudAgent => cloudAgent.RunStreamingAsync(userInput),
            LocalAgent localAgent => localAgent.RunStreamingAsync(userInput),
            AzureCopilotAgent copilotAgent => copilotAgent.RunStreamingAsync(userInput),
            _ => throw new InvalidOperationException("Unknown agent type")
        };
        
        await foreach (var update in streamingTask)
        {
            // Stop spinner on first update
            if (firstUpdate)
            {
                spinnerCts.Cancel();
                await spinnerTask;
                Console.Write("\r                    \r"); // Clear spinner line
                Console.ForegroundColor = ConsoleColor.Cyan;
                firstUpdate = false;
            }
            
            string updateText = update.ToString();
            Console.Write(updateText);
            responseBuilder.Append(updateText);
        }
        
        spinnerCts.Dispose();
        
        Console.ResetColor();
        Console.WriteLine();
        string fullResponse = responseBuilder.ToString();
        
        // Extract command if present (between ``` markers)
        var match = Regex.Match(fullResponse, @"```\s*\n(.*?)\n```", RegexOptions.Singleline);
        if (match.Success && string.IsNullOrEmpty(extractedCommand))
        {
            string potentialCommand = match.Groups[1].Value.Trim();
            
            // Check if command contains placeholders (angle brackets)
            bool hasPlaceholders = Regex.IsMatch(potentialCommand, @"<[^>]+>");
            
            if (!hasPlaceholders)
            {
                // This is a fully executable command - save it
                extractedCommand = potentialCommand;
            }
        }
        
        // Check if the agent is done
        if (fullResponse.Contains("[DONE]"))
        {
            // Check if agent confirmed or cancelled execution
            string responseLower = fullResponse.ToLowerInvariant();
            
            if (responseLower.Contains("cancelled") || 
                responseLower.Contains("won't execute") ||
                responseLower.Contains("not executing"))
            {
                // User declined execution
                extractedCommand = string.Empty;
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("=== Command execution cancelled ===");
                Console.ResetColor();
            }
            else if (!string.IsNullOrEmpty(extractedCommand))
            {
                // Command was confirmed for execution
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("=== Command ready to execute ===");
                Console.ResetColor();
            }
            else
            {
                // No executable command (e.g., syntax help with placeholders)
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("(Command syntax provided - contains placeholders)");
                Console.ResetColor();
            }
            break;
        }
        
        // Get next user input for continued conversation
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("\n> ");
        userInput = Console.ReadLine();
        
        if (string.IsNullOrWhiteSpace(userInput))
        {
            Console.WriteLine("No input provided. Exiting.");
            break;
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"Error: {ex.Message}");
        Console.ResetColor();
        break;
    }
}

Console.ResetColor();

// Final output: either the command or empty string
if (!string.IsNullOrEmpty(extractedCommand))
{
    Console.WriteLine();
    Console.WriteLine("=== FINAL COMMAND ===");
    Console.WriteLine(extractedCommand);
}
else
{
    Console.WriteLine();
    Console.WriteLine("=== NO COMMAND TO EXECUTE ===");
}
