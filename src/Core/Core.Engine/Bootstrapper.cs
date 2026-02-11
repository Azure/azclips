using Core.Abstractions;
using Core.Authentication;
using Core.Implementation;
using Core.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Engine;

/// <summary>
/// Bootstrapper for initializing and configuring the command execution engine.
///
/// Registers all services required for AI-powered command execution via AgentEngine.
/// The core pattern: static StartAsync() returns IServiceProvider,
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
    /// </summary>
    private static ServiceProvider RegisterServices()
    {
        var services = new ServiceCollection();

        // Core services
        services.AddSingleton<ILoggingService, ConsoleLoggingService>();
        services.AddSingleton<IAuthService, MsalAuthService>();
        services.AddSingleton<IAuthContextService, AuthContextService>();
        services.AddSingleton<ITelemetryService, TelemetryService>();

        // Output abstraction - allows supporting both Console (CLI) and PowerShell hosts
        services.AddSingleton<IOutputAdapter, ConsoleOutputAdapter>();

        // Agent engine - handles all input via AI (Azure OpenAI)
        // Supports natural language queries, typo correction, exploratory help, and command generation
        // Flavor is detected from environment variable set by PowerShell cmdlets
        services.AddSingleton<AgentEngine>(sp =>
        {
            var flavor = Environment.GetEnvironmentVariable("AZURECLI_FLAVOR") ?? "azure cli";
            return new AgentEngine(
                sp.GetRequiredService<IOutputAdapter>(),
                flavor);
        });

        return services.BuildServiceProvider();
    }
}
