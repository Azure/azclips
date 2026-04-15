using Core.Abstractions;
using Core.Runtime;
using Core.Authentication;
using Core.Telemetry;
using Core.Engine;
using Microsoft.Extensions.DependencyInjection;
using Core.AssemblyLoading;
using Core.ArgumentParsing;
using Core.Configuration;

namespace Core.DependencyInjection;

public static class CirrusServiceProvider
{
    private static volatile IServiceProvider? _serviceProvider;
    private static readonly object _lock = new();

    /// <summary>
    /// Optional hook invoked before the default service registrations.
    /// Set this from another assembly to prepend custom registrations into the DI container.
    /// Must be set before <see cref="Initialize"/> is called.
    /// </summary>
    public static Action<IServiceCollection>? PrependRegistration { get; set; }

    /// <summary>
    /// Optional hook invoked after the default service registrations.
    /// Set this from another assembly to override or replace default registrations in the DI container.
    /// Must be set before <see cref="Initialize"/> is called.
    /// </summary>
    public static Action<IServiceCollection>? OverrideRegistration { get; set; }

    /// <summary>
    /// Initializes the DI container. Safe to call multiple times — only the first call takes effect.
    /// Called during module import (psm1) or CLI startup.
    /// </summary>
    public static void Initialize(ClientType clientType)
    {
        if (_serviceProvider is not null)
            return;

        lock (_lock)
        {
            if (_serviceProvider is not null)
                return;

            IServiceCollection services = new ServiceCollection();

            //client type should be immutable and be initialized with composition root
            services.AddSingleton(typeof(ClientType), clientType);

            PrependRegistration?.Invoke(services);
            services = services
                .RegisterTelemetryServices()
                .RegisterRuntimeServices()
                .RegisterAuthenticationServices()
                .RegisterEngineServices()
                .RegisterAssemblyLoadingServices()
                .RegisterArgumentParsingServices()
                .RegisterConfigurationServices();
            OverrideRegistration?.Invoke(services);
            _serviceProvider = services.BuildServiceProvider();
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