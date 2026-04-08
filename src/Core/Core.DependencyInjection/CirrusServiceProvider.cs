using Core.Abstractions;
using Core.Runtime;
using Core.Authentication;
using Core.Telemetry;
using Core.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Core.DependencyInjection;

public static class CirrusServiceProvider
{
    private static volatile IServiceProvider? _serviceProvider;
    private static readonly object _lock = new();

    /// <summary>
    /// Initializes the DI container. Safe to call multiple times — only the first call takes effect.
    /// Called during module import (psm1) or CLI startup.
    /// </summary>
    public static void Initialize(ClientType clientType, string? serviceVersion = null)
    {
        if (_serviceProvider is not null)
            return;

        lock (_lock)
        {
            if (_serviceProvider is not null)
                return;

            _serviceProvider = new ServiceCollection()
                .RegisterRuntimeServices()
                .RegisterAuthenticationServices()
                .RegisterTelemetryServices(serviceVersion)
                .RegisterEngineServices(clientType)
                .BuildServiceProvider();
        }
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

    /// <summary>
    /// Resolves a service from the initialized container by Type.
    /// Use this overload from PowerShell, which cannot call generic methods directly.
    /// </summary>
    public static object GetRequiredService(Type serviceType)
    {
        return (_serviceProvider ?? throw new InvalidOperationException(
            "CirrusServiceProvider has not been initialized. Call Initialize() first."))
            .GetRequiredService(serviceType);
    }

    /// <summary>
    /// Disposes the initialized DI container and clears the cached provider.
    /// Safe to call multiple times.
    /// </summary>
    public static void Dispose()
    {
        lock (_lock)
        {
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _serviceProvider = null;
        }
    }
}