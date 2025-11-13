using System.Collections.Generic;
using Azure;
using Azure.AI.OpenAI;
using DotNetEnv;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

// Load .env file if it exists (for local development)
Env.Load();

// Load configuration from environment variables
string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") 
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable not set");
string apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") 
    ?? throw new InvalidOperationException("AZURE_OPENAI_API_KEY environment variable not set");
string deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") 
    ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT environment variable not set");

// Load prompt from file
string promptPath = Path.Combine(AppContext.BaseDirectory, "prompt.txt");
string instructions = File.Exists(promptPath)
    ? await File.ReadAllTextAsync(promptPath)
    : "You are a helpful assistant. Provide clear, concise answers.";

// Create the agent using API key authentication
AIAgent agent = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureKeyCredential(apiKey))
        .GetChatClient(deployment)
        .CreateAIAgent(
            instructions: instructions,
            name: "CLI Assistant");

// Conversation loop
var messages = new List<ChatMessage>();

Console.Write("Ask me anything: ");
string? userInput = Console.ReadLine();

if (string.IsNullOrWhiteSpace(userInput))
{
    Console.WriteLine("No question provided.");
    return;
}

messages.Add(new ChatMessage(ChatRole.User, userInput));

// Keep conversing until [DONE] marker is found
while (true)
{
    try
    {
        var response = await agent.RunAsync(messages);
        string responseText = response.ToString();
        
        Console.WriteLine($"\n{responseText}");
        
        // Check if the agent is done
        if (responseText.Contains("[DONE]"))
        {
            break;
        }
        
        // Get next user input
        Console.Write("\n> ");
        userInput = Console.ReadLine();
        
        if (string.IsNullOrWhiteSpace(userInput))
        {
            Console.WriteLine("No input provided. Exiting.");
            break;
        }
        
        messages.Add(new ChatMessage(ChatRole.Assistant, responseText));
        messages.Add(new ChatMessage(ChatRole.User, userInput));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        break;
    }
}
