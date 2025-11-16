using Core.Abstractions;
using Core.Implementation;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Engine;

public class Bootstrapper
{
    public static Task<ICommandEngine> StartAsync(CancellationToken cancellationToken = default)
    {
        var serviceProvider = RegisterServices();
        var telemetryService = serviceProvider.GetRequiredService<ITelemetryService>();
        telemetryService.Log("Core.Engine has started.");
        return Task.FromResult(serviceProvider.GetRequiredService<ICommandEngine>());
    }

    private static ServiceProvider RegisterServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ITelemetryService, ConsoleTelemetryService>();
        services.AddSingleton<ICommandEngine, DefaultCommandEngine>();

        return services.BuildServiceProvider();
    }
}
