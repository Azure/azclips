using Core.Abstractions;
using Core.Authentication;
using Core.Implementation;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Engine;

public class Bootstrapper
{
    public static Task<IServiceProvider> ServiceProviderAsync => _serviceProvider.Value;
    private static Lazy<Task<IServiceProvider>> _serviceProvider = new(() => StartAsync());

    private async static Task<IServiceProvider> StartAsync()
    {
        var serviceProvider = RegisterServices();
        var telemetryService = serviceProvider.GetRequiredService<ITelemetryService>();
        telemetryService.Log("Core.Engine has started.");
        return serviceProvider;
    }

    private static ServiceProvider RegisterServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ITelemetryService, ConsoleTelemetryService>();
        services.AddSingleton<ICommandEngine, DefaultCommandEngine>();
        services.AddSingleton<IAuthService, DefaultAuthService>();

        return services.BuildServiceProvider(); // The ServiceProvider returned by BuildServiceProvider() implements IDisposable but is never disposed. This can lead to resource leaks. Consider storing the provider and disposing it properly, or use BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true }) with proper lifetime management.
    }
}
