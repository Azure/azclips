using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using DotNetEnv;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Core.Agent;

/// <summary>
/// Azure Copilot Agent that connects to Azure CLI/PowerShell/Terraform Copilot API endpoints.
/// </summary>
public class AzureCopilotAgent
{
    private readonly HttpClient _httpClient;
    private readonly string _apiEndpoint;
    private readonly string _copilotType;
    private readonly string _pluginHeader;
    private readonly TokenCredential _credential;
    private readonly List<CopilotMessage> _conversationHistory;
    private AccessToken? _currentToken;
    private readonly string _conversationId;
    private readonly string _correlationId;

    // Resource ID for Azure Copilot API
    private const string CopilotResourceId = "api://a5ede409-60d3-4a6c-93e6-eb2e7271e8e3";

    private AzureCopilotAgent(
        HttpClient httpClient,
        string apiEndpoint,
        string copilotType,
        string pluginHeader,
        TokenCredential credential)
    {
        _httpClient = httpClient;
        _apiEndpoint = apiEndpoint;
        _copilotType = copilotType;
        _pluginHeader = pluginHeader;
        _credential = credential;
        _conversationHistory = new List<CopilotMessage>();
        _conversationId = Guid.NewGuid().ToString();
        _correlationId = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Creates an AzureCopilotAgent with configuration loaded from environment variables.
    /// </summary>
    /// <returns>A configured AzureCopilotAgent instance ready for conversation.</returns>
    public static async Task<AzureCopilotAgent> CreateAsync()
    {
        // Load .env file if it exists (for local development)
        Env.Load();

        // Load configuration from environment variables
        string env = Environment.GetEnvironmentVariable("AZURE_COPILOT_ENV") ?? "temp";
        string region = Environment.GetEnvironmentVariable("AZURE_COPILOT_REGION") ?? "eus";
        string copilotType = Environment.GetEnvironmentVariable("AZURE_COPILOT_TYPE") ?? "azps";

        // Validate environment
        if (!IsValidEnvironment(env))
        {
            throw new InvalidOperationException(
                $"Invalid AZURE_COPILOT_ENV: {env}. Valid values: local, temp, test, dev, beta, prod, ppe");
        }

        // Validate region
        if (!IsValidRegion(region))
        {
            throw new InvalidOperationException(
                $"Invalid AZURE_COPILOT_REGION: {region}. Valid values: eus, weu");
        }

        // Validate copilot type
        if (!IsValidCopilotType(copilotType))
        {
            throw new InvalidOperationException(
                $"Invalid AZURE_COPILOT_TYPE: {copilotType}. Valid values: azps, azcli, aztf");
        }

        // Build API endpoint
        string apiEndpoint = BuildApiEndpoint(env, region, copilotType);
        string pluginHeader = GetPluginHeader(copilotType);

        // Initialize Azure credential (tries env vars, Azure CLI, then interactive browser)
        var credential = AzureCopilotAuth.GetCredential();

        // Create HTTP client
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120)
        };

        var agent = new AzureCopilotAgent(httpClient, apiEndpoint, copilotType, pluginHeader, credential);

        // Pre-fetch and validate token
        await agent.EnsureValidTokenAsync();

        // Initialize conversation with system prompt
        await agent.InitializeConversationAsync();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"✓ Azure Copilot Agent initialized ({copilotType.ToUpper()})");
        Console.WriteLine($"  Environment: {env}");
        Console.WriteLine($"  Region: {region}");
        Console.WriteLine($"  Endpoint: {apiEndpoint}");
        Console.ResetColor();

        return agent;
    }

    /// <summary>
    /// Runs the agent with streaming response, accumulating conversation history.
    /// </summary>
    public async IAsyncEnumerable<AgentRunResponseUpdate> RunStreamingAsync(string userInput)
    {
        // Add user message to conversation history
        _conversationHistory.Add(new CopilotMessage { Role = "user", Content = userInput });

        // Ensure we have a valid token
        await EnsureValidTokenAsync();

        // Build request body based on copilot type
        object requestBody = BuildRequestBody(userInput);

        // Create HTTP request
        using var request = new HttpRequestMessage(HttpMethod.Post, _apiEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _currentToken!.Value.Token);
        request.Headers.Add("x-ms-conversation-id", _conversationId);
        request.Headers.Add("x-ms-correlation-id", _correlationId);
        request.Headers.Add("x-ms-plugin", _pluginHeader);
        request.Headers.Add("x-ms-agent", "");
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        // Send request and stream response
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        // Read response content
        var responseContent = await response.Content.ReadAsStringAsync();
        
        // Parse response based on copilot type
        string assistantMessage = ParseResponse(responseContent);

        // Add assistant response to conversation history
        _conversationHistory.Add(new CopilotMessage { Role = "assistant", Content = assistantMessage });

        // Create a simple chat client that wraps our response to get proper AgentRunResponseUpdate objects
        var tempChatClient = new SingleMessageChatClient(assistantMessage);
        var tempAgent = tempChatClient.CreateAIAgent(instructions: "");
        
        await foreach (var update in tempAgent.RunStreamingAsync(new List<ChatMessage>()))
        {
            yield return update;
        }
    }

    /// <summary>
    /// Simple IChatClient implementation that returns a single predefined message.
    /// Used to convert string responses into AgentRunResponseUpdate format.
    /// </summary>
    private sealed class SingleMessageChatClient : IChatClient
    {
        private readonly string _message;

        public SingleMessageChatClient(string message)
        {
            _message = message;
        }

        public ChatClientMetadata Metadata => new("SingleMessage", null);

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _message)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, _message);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public TService? GetService<TService>(object? key = null) where TService : class => this as TService;

        public void Dispose() { }
    }

    /// <summary>
    /// Gets the current conversation history.
    /// </summary>
    public IReadOnlyList<CopilotMessage> ConversationHistory => _conversationHistory.AsReadOnly();

    /// <summary>
    /// Clears the conversation history.
    /// </summary>
    public void ClearHistory()
    {
        _conversationHistory.Clear();
    }

    #region Private Helper Methods

    /// <summary>
    /// Initializes the conversation by sending the system prompt as the first user message.
    /// This establishes the conversation guidelines and principles.
    /// </summary>
    private async Task InitializeConversationAsync()
    {
        // Load prompt from file
        string promptPath = Path.Combine(AppContext.BaseDirectory, "prompt.txt");
        if (!File.Exists(promptPath))
        {
            return; // Skip initialization if no prompt file exists
        }

        string systemPrompt = await File.ReadAllTextAsync(promptPath);
        
        // Compose initialization message
        string initMessage = $@"Hello! I'm setting up our conversation. Please acknowledge these principles that will govern our interaction:

{systemPrompt}

Please confirm you understand these guidelines and are ready to assist me with Azure commands.";

        // Send initialization message to the endpoint
        await SendInitializationMessageAsync(initMessage);
    }

    /// <summary>
    /// Sends the initialization message to the endpoint without yielding to the user.
    /// This primes the conversation with the system prompt.
    /// </summary>
    private async Task SendInitializationMessageAsync(string message)
    {
        // Add initialization message to conversation history
        _conversationHistory.Add(new CopilotMessage { Role = "user", Content = message });

        // Ensure we have a valid token
        await EnsureValidTokenAsync();

        // Build request body based on copilot type
        object requestBody = BuildRequestBody(message);

        // Create HTTP request
        using var request = new HttpRequestMessage(HttpMethod.Post, _apiEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _currentToken!.Value.Token);
        request.Headers.Add("x-ms-conversation-id", _conversationId);
        request.Headers.Add("x-ms-correlation-id", _correlationId);
        request.Headers.Add("x-ms-plugin", _pluginHeader);
        request.Headers.Add("x-ms-agent", "");
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        // Send request and get response
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        // Read response content
        var responseContent = await response.Content.ReadAsStringAsync();
        
        // Parse response based on copilot type
        string assistantMessage = ParseResponse(responseContent);

        // Add assistant response to conversation history
        _conversationHistory.Add(new CopilotMessage { Role = "assistant", Content = assistantMessage });
    }

    private async Task EnsureValidTokenAsync()
    {
        // Check if we need to refresh the token (if it's null or expiring soon)
        if (_currentToken == null || _currentToken.Value.ExpiresOn <= DateTimeOffset.UtcNow.AddMinutes(5))
        {
            var tokenRequestContext = new TokenRequestContext(new[] { $"{CopilotResourceId}/.default" });
            _currentToken = await _credential.GetTokenAsync(tokenRequestContext, default);
        }
    }

    private static string BuildApiEndpoint(string env, string region, string copilotType)
    {
        if (env == "local")
        {
            return copilotType switch
            {
                "azps" => "http://127.0.0.1:8000/api/azure-powershell/copilot",
                "azcli" => "http://127.0.0.1:8000/api/CopilotService",
                "aztf" => "http://127.0.0.1:8000/copilot",
                _ => "http://127.0.0.1:8000/copilot"
            };
        }

        string baseUri;
        if (env is "beta" or "prod" or "ppe")
        {
            baseUri = $"https://azclis-copilot-apim-{env}-{region}.azure-api.net";
        }
        else
        {
            baseUri = $"https://azclitools-copilot-apim-{env}.azure-api.net";
        }

        return $"{baseUri}/{copilotType}/copilot";
    }

    private static string GetPluginHeader(string copilotType)
    {
        return copilotType switch
        {
            "azps" => "generate_powershell_scripts_plugin",
            "azcli" => "generate_cli_scripts_plugin",
            "aztf" => "generate_terraform_config_plugin",
            _ => throw new ArgumentException($"Unknown copilot type: {copilotType}")
        };
    }

    private object BuildRequestBody(string question)
    {
        if (_copilotType == "azcli")
        {
            return new { question };
        }
        
        // For azps and aztf, use messages format
        return new
        {
            messages = new[]
            {
                new { role = "user", content = question }
            }
        };
    }

    private string ParseResponse(string responseContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            if (_copilotType == "azcli")
            {
                // For azcli, the entire response is the content
                return responseContent;
            }

            // For azps and aztf, extract the 'content' field
            if (root.TryGetProperty("content", out var contentElement))
            {
                return contentElement.GetString() ?? "No response content";
            }

            return "No response content";
        }
        catch (JsonException)
        {
            // If parsing fails, return the raw content
            return responseContent;
        }
    }

    private static bool IsValidEnvironment(string env)
    {
        return env is "local" or "temp" or "test" or "dev" or "beta" or "prod" or "ppe";
    }

    private static bool IsValidRegion(string region)
    {
        return region is "eus" or "weu";
    }

    private static bool IsValidCopilotType(string copilotType)
    {
        return copilotType is "azps" or "azcli" or "aztf";
    }

    #endregion
}

/// <summary>
/// Represents a message in the Azure Copilot conversation.
/// </summary>
public class CopilotMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}
