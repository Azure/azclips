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

        services.AddSingleton<HttpClient>();
        services.AddSingleton<IEngine, Engine>();

        return services;
    }
}
