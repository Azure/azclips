using Core.Agent;
using System.Text;

namespace Core.Agent.Standalone;

/// <summary>
/// Minimal standalone executable for agent testing and evaluation.
/// Directly uses CloudAgent without production infrastructure (no Bootstrapper, Engine, etc.)
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        ConsoleHelpers.WriteCyan("Azure CLI Agent - Standalone Mode\n==================================");

        // Parse backend and flavor
        var (backend, flavor) = ParseArgs(args);
        
        if (backend == "local" || backend == "copilot")
        {
            ConsoleHelpers.WriteCyan($"{backend} backend is not yet implemented in this branch.\nThe implementation exists in dev/agent branch.");
            return 1;
        }
        
        ConsoleHelpers.WriteCyan($"Backend: {backend}\nFlavor: {flavor}");

        // Create CloudAgent directly (no Bootstrapper, no Engine)
        var agent = await CloudAgent.CreateAsync(flavor);

        ConsoleHelpers.WriteCyan("Enter natural language commands (or 'quit' to exit):\nExamples:\n  - show me all resource groups\n  - how do I create a VM\n  - grup list (typo correction)");

        // Interactive loop
        var responseBuilder = new StringBuilder();
        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            try
            {
                Console.WriteLine();

                // Accumulate response to check for exit
                responseBuilder.Clear();

                // Show spinner while streaming response
                using (Spinner.Start())
                {
                    await foreach (var chunk in agent.RunStreamingAsync(input))
                    {
                        responseBuilder.Append(chunk);
                    }
                } // Spinner automatically disposed here

                // Parse response and extract the "response" field to display
                var fullResponse = responseBuilder.ToString();
                if (AgentResponseParser.TryParse(fullResponse, out var type, out var response))
                {
                    // Display the response in cyan (like AgentEngine does)
                    ConsoleHelpers.WriteCyan(response!);

                    // Check if agent returned exit type
                    if (type == "exit")
                    {
                        break;
                    }
                }
                else
                {
                    // Fallback: display raw response if JSON parsing fails
                    ConsoleHelpers.WriteCyan(fullResponse);
                }
            }
            catch (Exception ex)
            {
                ConsoleHelpers.WriteCyan($"Error: {ex.Message}");
            }
        }

        ConsoleHelpers.WriteCyan("Goodbye!");
        return 0;
    }

    static (string backend, string flavor) ParseArgs(string[] args)
    {
        var backend = "Azure OpenAI";
        var flavor = "azure cli";

        if (args.Contains("--local-model"))
        {
            backend = "local";
        }
        else if (args.Contains("--use-copilot"))
        {
            backend = "copilot";
        }

        if (args.Contains("--powershell") || args.Contains("--ps"))
        {
            flavor = "azure powershell";
        }

        return (backend, flavor);
    }
}
