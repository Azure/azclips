using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Telemetry;

public static class DIRegisterExtensions
{
    /// <summary>
    /// Extension method to register telemetry-related services.
    /// </summary>
    public static IServiceCollection RegisterTelemetryServices(this IServiceCollection services)
    {
        services.AddSingleton<ITelemetryService>(_ => new TelemetryService());
        return services;
    }
}