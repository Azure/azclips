using System.ClientModel;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using DotNetEnv;
using OpenAI.Chat;

namespace Cirrus.Autocomplete.Agent;

/// <summary>
/// Dedicated autocomplete agent. Calls a remote LLM (e.g. gpt-5x-nano)
/// with a strict JSON contract and returns a list of suggestion items.
/// Does not reuse Core.Agent.CloudAgent or AgentEngine.
/// </summary>
internal sealed class AutocompleteAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _endpoint;
    private readonly string _deployment;
    private readonly string? _apiKey;
    private readonly string _instructions;

    private AutocompleteAgent(string endpoint, string deployment, string? apiKey, string instructions)
    {
        _endpoint = endpoint;
        _deployment = deployment;
        _apiKey = apiKey;
        _instructions = instructions;
    }

    public static AutocompleteAgent? TryCreate()
    {
        LoadDotEnvIfPresent();

        string? endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_AUTOCOMPLETE_ENDPOINT")
            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        string? deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_AUTOCOMPLETE_DEPLOYMENT")
            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");
        string? apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_AUTOCOMPLETE_API_KEY")
            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment))
        {
            return null;
        }

        string instructions = LoadInstructions();
        return new AutocompleteAgent(endpoint, deployment, apiKey, instructions);
    }

    public async Task<List<SuggestionItem>> GetSuggestionsAsync(CompletionRequest request, CancellationToken cancellationToken)
    {
        AzureOpenAIClient client = string.IsNullOrWhiteSpace(_apiKey)
            ? throw new InvalidOperationException("AZURE_OPENAI_AUTOCOMPLETE_API_KEY (or AZURE_OPENAI_API_KEY) is required for iteration 1.")
            : new AzureOpenAIClient(new Uri(_endpoint), new ApiKeyCredential(_apiKey));

        ChatClient chatClient = client.GetChatClient(_deployment);

        string userPayload = JsonSerializer.Serialize(request, JsonOptions);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(_instructions),
            new UserChatMessage(userPayload),
        };

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
        };

        ClientResult<ChatCompletion> result = await chatClient.CompleteChatAsync(messages, options, cancellationToken).ConfigureAwait(false);
        ChatCompletion completion = result.Value;

        string? text = completion.Content?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<SuggestionItem>();
        }

        SuggestionResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<SuggestionResponse>(text, JsonOptions);
        }
        catch (JsonException)
        {
            return new List<SuggestionItem>();
        }

        if (parsed?.Items == null)
        {
            return new List<SuggestionItem>();
        }

        return Sanitize(parsed.Items, request.Prefix);
    }

    private static List<SuggestionItem> Sanitize(List<SuggestionItem> items, string prefix)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var clean = new List<SuggestionItem>(items.Count);

        foreach (SuggestionItem item in items)
        {
            string value = item.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(value) || value.Contains(' '))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(prefix) && !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!seen.Add(value))
            {
                continue;
            }

            item.Value = value;
            if (string.IsNullOrEmpty(item.Display))
            {
                item.Display = value;
            }

            clean.Add(item);
        }

        return clean;
    }

    private static string LoadInstructions()
    {
        string promptPath = Path.Combine(AppContext.BaseDirectory, "Prompts", "autocomplete.prompt.md");
        if (File.Exists(promptPath))
        {
            return File.ReadAllText(promptPath);
        }

        return "You are an autocomplete engine. Return JSON: {\"items\":[]}";
    }

    private static void LoadDotEnvIfPresent()
    {
        string[] candidates =
        {
            Path.Combine(AppContext.BaseDirectory, ".env"),
            Path.Combine(Directory.GetCurrentDirectory(), ".env"),
        };

        foreach (string path in candidates)
        {
            if (File.Exists(path))
            {
                Env.Load(path);
                return;
            }
        }
    }
}
