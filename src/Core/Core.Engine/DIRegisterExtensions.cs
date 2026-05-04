using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Engine;

public static class DIRegisterExtensions
{
    /// <summary>
    /// Extension method to register engine-related services.
    /// </summary>
    public static IServiceCollection RegisterEngineServices(this IServiceCollection services)
    {
        // Command parser - extracts commands and options from string[] args
        // Originally instantiated inline in DefaultCommandEngine; now registered
        // as service for testability and potential reuse across engines
        services.AddSingleton<CommandParser>();

        // Command binder - dynamically loads modules and binds commands
        // Originally instantiated inline in DefaultCommandEngine; extracted
        // to service for testability and explicit dependency management
        services.AddSingleton<CommandBinder>();

        // Default command engine - handles structured commands (e.g., "group create --name test")
        // This is the original ICommandEngine implementation, now one of multiple engines
        services.AddSingleton<DefaultCommandEngine>();

        // Agent engine - handles natural language input via AI (Azure OpenAI)
        // NEW: This didn't exist in original design. Added to support queries like
        // "create a resource group called test" or "how do I list my groups?"
        // Flavor is detected from environment variable set by PowerShell cmdlets
        services.AddSingleton<AgentEngine>(sp =>
        {
            var flavor = Environment.GetEnvironmentVariable("AZURECLI_FLAVOR") ?? "azure cli";
            return new AgentEngine(
                sp.GetRequiredService<IOutputAdapter>(),
                flavor);
        });

        // Orchestrator engine - tries DefaultEngine first, falls back to AgentEngine on failure
        // NEW: This is the actual ICommandEngine returned to Program.cs
        // Implements the "smart fallback" pattern: structured commands work as before,
        // but typos, unknown commands, or natural language automatically invoke AI
        // This is why we changed from simple DefaultCommandEngine to OrchestratorEngine
        services.AddSingleton<ICommandEngine, OrchestratorEngine>(sp =>
            new OrchestratorEngine(
                sp.GetRequiredService<DefaultCommandEngine>(),
                sp.GetRequiredService<AgentEngine>(),
                sp.GetRequiredService<CommandParser>(),
                sp.GetRequiredService<CommandBinder>(),
                sp.GetRequiredService<ILoggingService>(),
                sp.GetRequiredService<IOutputAdapter>()));

        services.AddSingleton<HttpClient>();
        services.AddSingleton<IEngine, Engine>();

        return services;
    }
}
