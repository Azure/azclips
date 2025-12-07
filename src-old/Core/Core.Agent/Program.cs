using Core.Agent;

// Parse command-line arguments
bool useCopilot = args.Contains("--use-copilot");
bool useLocalModel = args.Contains("--local-model");

// Create the agent based on the mode (priority: --use-copilot > --local-model > cloud)
object agent;
if (useCopilot)
{
    Console.WriteLine("Initializing Azure Copilot agent...");
    agent = await AzureCopilotAgent.CreateAsync();
}
else if (useLocalModel)
{
    Console.WriteLine("Initializing local Phi-3.5-mini model...");
    agent = await LocalAgent.CreateAsync();
}
else
{
    agent = await CloudAgent.CreateAsync();
}

// Conversation loop
Console.ForegroundColor = ConsoleColor.White;
Console.Write("Ask me anything: ");
string? userInput = Console.ReadLine();

if (string.IsNullOrWhiteSpace(userInput))
{
    Console.WriteLine("No question provided.");
    return;
}

// Keep conversing until [DONE] marker is found
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
        
        // Check if the agent is done
        if (fullResponse.Contains("[DONE]"))
        {
            break;
        }
        
        // Get next user input
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
