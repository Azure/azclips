using Azure;
using Azure.AI.OpenAI;
using DotNetEnv;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Core.Agent;

/// <summary>
/// Cloud-based AI Agent that connects to Azure OpenAI.
/// </summary>
public class CloudAgent
{
    private readonly AIAgent _innerAgent;
    private readonly List<ChatMessage> _conversationHistory;

    private CloudAgent(AIAgent innerAgent)
    {
        _innerAgent = innerAgent;
        _conversationHistory = new List<ChatMessage>();
    }

    /// <summary>
    /// Creates a CloudAgent with configuration loaded from environment variables.
    /// </summary>
    /// <param name="flavor">The command flavor ("azure cli" or "azure powershell")</param>
    /// <returns>A configured CloudAgent instance ready for conversation.</returns>
    public static async Task<CloudAgent> CreateAsync(string flavor)
    {
        // Try to load .env file from multiple locations
        // 1. Application directory (for CLI usage)
        // 2. Current working directory (for any usage)
        // 3. Module directory (for PowerShell module usage - get from assembly location)
        var envLocations = new[]
        {
            Path.Combine(AppContext.BaseDirectory, ".env"),
            Path.Combine(Directory.GetCurrentDirectory(), ".env"),
            Path.Combine(Path.GetDirectoryName(typeof(CloudAgent).Assembly.Location) ?? "", ".env"),
            Path.Combine(Directory.GetCurrentDirectory(), "bin", ".env")
        };

        foreach (var envPath in envLocations)
        {
            if (File.Exists(envPath))
            {
                Env.Load(envPath);
                break;
            }
        }

        // Load configuration from environment variables
        string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") 
            ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable not set. Set AZURE_OPENAI_ENDPOINT in your .env file or as an environment variable. See .env.example for reference.");
        string apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") 
            ?? throw new InvalidOperationException("AZURE_OPENAI_API_KEY environment variable not set. Set AZURE_OPENAI_API_KEY in your .env file or as an environment variable. See .env.example for reference.");
        string deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") 
            ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT environment variable not set. Set AZURE_OPENAI_DEPLOYMENT in your .env file or as an environment variable. See .env.example for reference.");

        // Load prompt from file and replace {{flavor}} placeholder
        string promptPath = Path.Combine(AppContext.BaseDirectory, "prompt.txt");
        string instructions = File.Exists(promptPath)
            ? await File.ReadAllTextAsync(promptPath)
            : "You are a helpful assistant. Provide clear, concise answers.";
        
        // Replace {{flavor}} with the actual flavor
        instructions = instructions.Replace("{{flavor}}", flavor);

        // Create the agent using API key authentication
        AIAgent innerAgent = new AzureOpenAIClient(
            new Uri(endpoint),
            new AzureKeyCredential(apiKey))
                .GetChatClient(deployment)
                .CreateAIAgent(
                    instructions: instructions,
                    name: "CLI Assistant");

        return new CloudAgent(innerAgent);
    }

    /// <summary>
    /// Runs the agent with streaming response, accumulating conversation history.
    /// </summary>
    public async IAsyncEnumerable<AgentRunResponseUpdate> RunStreamingAsync(string userInput)
    {
        // Add user message to conversation history
        var userMessage = new ChatMessage(ChatRole.User, userInput);
        _conversationHistory.Add(userMessage);

        // Stream the response and accumulate it
        var responseBuilder = new System.Text.StringBuilder();
        await foreach (var update in _innerAgent.RunStreamingAsync(_conversationHistory))
        {
            responseBuilder.Append(update.ToString());
            yield return update;
        }

        // Add assistant's response to conversation history
        var assistantMessage = new ChatMessage(ChatRole.Assistant, responseBuilder.ToString());
        _conversationHistory.Add(assistantMessage);
    }

    /// <summary>
    /// Gets the current conversation history.
    /// </summary>
    public IReadOnlyList<ChatMessage> ConversationHistory => _conversationHistory.AsReadOnly();

    /// <summary>
    /// Clears the conversation history.
    /// </summary>
    public void ClearHistory()
    {
        _conversationHistory.Clear();
    }
}
