using Core.Abstractions;
using Core.Authentication;
using Core.Implementation;
using Core.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Engine;

/// <summary>
/// Bootstrapper for initializing and configuring the command execution engine.
///
/// DESIGN EVOLUTION:
/// Original design (main branch) was minimal - only registered ILogger and ICommandEngine.
/// This was sufficient for a simple CLI that parsed commands and executed them directly.
///
/// Current design extends the original pattern to support:
/// 1. AI-powered natural language processing via AgentEngine
/// 2. Orchestration pattern that tries structured commands first, falls back to AI
/// 3. Output abstraction to support both CLI (Console) and PowerShell hosts
/// 4. Parser and Binder as explicit services for better testability
///
/// The core pattern remains unchanged: static StartAsync() returns ICommandEngine,
/// all services registered as Singletons (appropriate for short-lived CLI process).
/// </summary>
public class Bootstrapper
{
    public static Task<IServiceProvider> ServiceProviderAsync => _serviceProvider.Value;
    private readonly static Lazy<Task<IServiceProvider>> _serviceProvider = new(() => StartAsync());

    private static async Task<IServiceProvider> StartAsync()
    {
        return await Task.Run(() =>
        {
            var serviceProvider = RegisterServices();
            var Logger = serviceProvider.GetRequiredService<ILoggingService>();
            Logger.Verbose($"[{nameof(Bootstrapper)}] Core.Engine has started.");
            return serviceProvider;
        });
    }

    /// <summary>
    /// Registers all services required for command execution.
    ///
    /// All services are registered as Singleton because:
    /// - CLI process is short-lived (single command execution)
    /// - No per-request scoping needed
    /// - Matches original design pattern from main branch
    /// </summary>
    private static ServiceProvider RegisterServices()
    {
        var services = new ServiceCollection();

        // --- ORIGINAL REGISTRATIONS (from main branch) ---
        // Telemetry service for logging throughout the application
        services.AddSingleton<ILoggingService, ConsoleLoggingService>();
        services.AddSingleton<IAuthService, MsalAuthService>();
        services.AddSingleton<IAuthContextService, AuthContextService>();
        services.AddSingleton<ITelemetryService, TelemetryService>();

        // --- NEW REGISTRATIONS (added for AI integration) ---

        // Output abstraction - allows supporting both Console (CLI) and PowerShell hosts
        // Original design used Console.WriteLine directly; this abstraction enables
        // future PowerShell output formatting without changing engine logic
        services.AddSingleton<IOutputAdapter, ConsoleOutputAdapter>();

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

        // Engine factory - creates ICommandEngine instances based on client type
        // NEW: Supports multiple client types (CLI, PowerShell) and engine selection
        services.AddSingleton<EngineFactory>();

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

        return services.BuildServiceProvider();
        // Note: ServiceProvider is not disposed. For a short-lived CLI process that exits
        // immediately after command execution, OS cleans up resources. In a long-running
        // PowerShell session, the module lifetime is managed by PowerShell, not this bootstrapper.
    }
}
