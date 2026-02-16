using Microsoft.Extensions.DependencyInjection;

namespace Core.Agent;

public static class DIRegisterExtensions
{
    /// <summary>
    /// Extension method to register agent-related services.
    /// </summary>
    public static IServiceCollection RegisterAgentServices(this IServiceCollection services)
    {
        //TODO: Register any agent-specific services here.
        return services;
    }
}