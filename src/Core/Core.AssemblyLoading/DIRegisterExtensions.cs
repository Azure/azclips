using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Core.AssemblyLoading;

public static class DIRegisterExtensions
{
    /// <summary>
    /// Extension method to register assembly loading-related services.
    /// </summary>
    public static IServiceCollection RegisterAssemblyLoadingServices(this IServiceCollection services)
    {
        services.AddSingleton<IAssemblyLoadingProvider, DefaultAssemblyLoadingProvider>();
        services.AddSingleton<IAssemblyLoadingService, AssemblyLoadingService>();
        return services;
    }
}