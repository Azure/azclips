using System.Diagnostics;
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
        services.AddSingleton<ITelemetryService>(sp =>
        {
            var clientType = sp.GetRequiredService<ClientType>();
            var configService = sp.GetRequiredService<IConfigurationService>();
            var telemetryOptions = configService.GetConfiguration<TelemetryOptions>();
            return new TelemetryService(clientType, telemetryOptions);
        });
        return services;
    }
}