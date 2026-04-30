using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Runtime;

public static class DIRegisterExtensions
{
    /// <summary>
    /// Extension method to register runtime-related services.
    /// </summary>
    public static IServiceCollection RegisterRuntimeServices(this IServiceCollection services)
    {
        // Output abstraction - allows supporting both Console (CLI) and PowerShell hosts
        // Original design used Console.WriteLine directly; this abstraction enables
        // future PowerShell output formatting without changing engine logic
        services.AddSingleton<IOutputAdapter, ConsoleOutputAdapter>();
        services.AddSingleton<ILoggingService, ScopedLoggingService>();
        return services;
    }
}