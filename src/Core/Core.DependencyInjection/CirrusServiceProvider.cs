using Core.Abstractions;
using Core.Runtime;
using Core.Authentication;
using Core.Telemetry;
using Core.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Core.DependencyInjection;

public static class CirrusServiceProvider
{
    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// Initializes the DI container. Called once during module import (psm1) or CLI startup.
    /// </summary>
    public static void Initialize(ClientType clientType)
    {
        if (_serviceProvider is not null)
            throw new InvalidOperationException("CirrusServiceProvider is already initialized.");

        _serviceProvider = new ServiceCollection()
            .RegisterRuntimeServices()
            .RegisterAuthenticationServices()
            .RegisterTelemetryServices()
            .RegisterEngineServices(clientType)
            .BuildServiceProvider();
    }

    /// <summary>
    /// Resolves a service from the initialized container.
    /// </summary>
    public static T GetRequiredService<T>() where T : notnull
    {
        return (_serviceProvider ?? throw new InvalidOperationException(
            "CirrusServiceProvider has not been initialized. Call Initialize() first."))
            .GetRequiredService<T>();
    }
}