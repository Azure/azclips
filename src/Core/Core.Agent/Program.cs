using Core.Agent;

// Create the agent
CloudAgent agent = await CloudAgent.CreateAsync();

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
                // Expected when cancelled
            }
        }, spinnerCts.Token);
        
        // Stream the response (conversation history is managed inside CloudAgent)
        var responseBuilder = new System.Text.StringBuilder();
        bool firstUpdate = true;
        
        await foreach (var update in agent.RunStreamingAsync(userInput))
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
