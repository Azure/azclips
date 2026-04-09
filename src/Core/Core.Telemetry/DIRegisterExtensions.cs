using System.Diagnostics;
using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Telemetry;

public static class DIRegisterExtensions
{
    /// <summary>
    /// Extension method to register telemetry-related services.
    /// </summary>
    public static IServiceCollection RegisterTelemetryServices(this IServiceCollection services, ClientType clientType)
    {
        services.AddSingleton(_ => new ActivitySource(clientType.ToString()));
        services.AddSingleton<ITelemetryService, TelemetryService>();
        return services;
    }
}