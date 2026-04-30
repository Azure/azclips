using Azure;
using Azure.AI.OpenAI;
using DotNetEnv;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Cirrus.Autocomplete.Agent;

/// <summary>
/// Dedicated autocomplete agent. Mirrors the shape of Core.Agent.CloudAgent,
/// but with autocomplete-specific configuration and no conversation history.
/// </summary>
internal sealed class AutocompleteAgent
{
    private readonly AIAgent _innerAgent;

    private AutocompleteAgent(AIAgent innerAgent)
    {
        _innerAgent = innerAgent;
    }

    /// <summary>
    /// Creates an AutocompleteAgent with configuration loaded from environment variables.
    /// Throws InvalidOperationException with a descriptive message if required configuration is missing.
    /// </summary>
    public static async Task<AutocompleteAgent> CreateAsync()
    {
        // Try to load .env file from common locations (mirrors CloudAgent).
        var envLocations = new[]
        {
            Path.Combine(AppContext.BaseDirectory, ".env"),
            Path.Combine(Directory.GetCurrentDirectory(), ".env"),
            Path.Combine(Path.GetDirectoryName(typeof(AutocompleteAgent).Assembly.Location) ?? "", ".env"),
        };

        string? loadedFrom = null;
        foreach (var envPath in envLocations)
        {
            if (File.Exists(envPath))
            {
                Env.Load(envPath);
                loadedFrom = envPath;
                break;
            }
        }

        // Autocomplete-specific env vars (no fallback to shared CLI vars,
        // so autocomplete cannot accidentally use the heavier command-agent model).
        string? endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_AUTOCOMPLETE_ENDPOINT");
        string? apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_AUTOCOMPLETE_API_KEY");
        string? deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_AUTOCOMPLETE_DEPLOYMENT");

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(endpoint)) missing.Add("AZURE_OPENAI_AUTOCOMPLETE_ENDPOINT");
        if (string.IsNullOrWhiteSpace(apiKey)) missing.Add("AZURE_OPENAI_AUTOCOMPLETE_API_KEY");
        if (string.IsNullOrWhiteSpace(deployment)) missing.Add("AZURE_OPENAI_AUTOCOMPLETE_DEPLOYMENT");

        if (missing.Count > 0)
        {
            string searched = string.Join(", ", envLocations);
            string foundMsg = loadedFrom is null
                ? $"no .env file found (searched: {searched})"
                : $".env loaded from {loadedFrom}";
            throw new InvalidOperationException(
                $"Missing autocomplete configuration: {string.Join(", ", missing)}. {foundMsg}.");
        }

        // Load prompt from file (mirrors CloudAgent).
        string promptPath = Path.Combine(AppContext.BaseDirectory, "Prompts", "autocomplete.prompt.md");
        string instructions = File.Exists(promptPath)
            ? await File.ReadAllTextAsync(promptPath)
            : "You are an autocomplete engine. Return JSON: {\"items\":[]}";

        AIAgent innerAgent = new AzureOpenAIClient(
            new Uri(endpoint!),
            new AzureKeyCredential(apiKey!))
                .GetChatClient(deployment)
                .CreateAIAgent(
                    instructions: instructions,
                    name: "Autocomplete Assistant");

        return new AutocompleteAgent(innerAgent);
    }

    /// <summary>
    /// Runs the agent for a single completion request and returns the raw model text.
    /// No conversation history is kept. Output is capped to keep latency low —
    /// autocomplete only needs a small JSON payload of suggestions.
    /// Override the cap with AZV2_AUTOCOMPLETE_MAX_OUTPUT_TOKENS.
    /// </summary>
    public async Task<string> RunAsync(string userInput, CancellationToken cancellationToken)
    {
        var runOptions = new ChatClientAgentRunOptions(new ChatOptions
        {
            MaxOutputTokens = GetMaxOutputTokens(),
        });

        AgentRunResponse response = await _innerAgent
            .RunAsync(userInput, options: runOptions, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response.ToString() ?? string.Empty;
    }

    private static int GetMaxOutputTokens()
    {
        string? raw = Environment.GetEnvironmentVariable("AZV2_AUTOCOMPLETE_MAX_OUTPUT_TOKENS");
        if (int.TryParse(raw, out int value) && value > 0)
        {
            return value;
        }
        return 256;
    }
}
