// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Runtime;
using Core.Authentication;
using Core.Telemetry;
using Core.Engine;
using Microsoft.Extensions.DependencyInjection;
using Core.AssemblyLoading;
using Core.ArgumentParsing;
using Core.Configuration;
using System.Collections.Concurrent;

namespace Core.DependencyInjection;

public static class AzclipsServiceProvider
{
    private static volatile IServiceProvider? _serviceProvider;
    private static readonly object _lock = new();
    private static readonly ConcurrentQueue<Action<IServiceCollection>> _prependRegistrations = new();
    private static readonly ConcurrentQueue<Action<IServiceCollection>> _overrideRegistrations = new();

    /// <summary>
    /// Adds a registration action invoked before the default service registrations.
    /// Multiple registrations are applied in insertion order.
    /// Must be called before <see cref="Initialize"/>.
    /// </summary>
    public static void PrependRegistration(Action<IServiceCollection>? registration)
    {
        if (registration is null)
            return;
        if (_serviceProvider is not null)
            throw new InvalidOperationException("Cannot set PrependRegistration after the service provider has been initialized.");
        _prependRegistrations.Enqueue(registration);
    }

    /// <summary>
    /// Adds a registration action invoked after the default service registrations.
    /// Multiple registrations are applied in insertion order; DI last-wins semantics
    /// means later registrations for the same service type take precedence.
    /// Must be called before <see cref="Initialize"/>.
    /// </summary>
    public static void OverrideRegistration(Action<IServiceCollection>? registration)
    {
        if (registration is null)
            return;
        if (_serviceProvider is not null)
            throw new InvalidOperationException("Cannot set OverrideRegistration after the service provider has been initialized.");
        _overrideRegistrations.Enqueue(registration);
    }

    public static void RegisterAzclipsServices(this IServiceCollection services) =>
        services
            .RegisterConfigurationServices()
            .RegisterTelemetryServices()
            .RegisterRuntimeServices()
            .RegisterAuthenticationServices()
            .RegisterEngineServices()
            .RegisterAssemblyLoadingServices()
            .RegisterArgumentParsingServices();

    /// <summary>
    /// Initializes the DI container. Safe to call multiple times — only the first call takes effect.
    /// Called during module import (psm1) or CLI startup.
    /// </summary>
    public static void Initialize(ClientMetadata clientMetadata)
    {
        ArgumentNullException.ThrowIfNull(clientMetadata);

        if (_serviceProvider is not null)
            return;

        lock (_lock)
        {
            if (_serviceProvider is not null)
                return;

            IServiceCollection services = new ServiceCollection();
            services.AddSingleton(clientMetadata);
            while (_prependRegistrations.TryDequeue(out var prependRegistration))
            {
                prependRegistration?.Invoke(services);
            }
            services.RegisterAzclipsServices();
            while (_overrideRegistrations.TryDequeue(out var overrideRegistration))
            {
                overrideRegistration?.Invoke(services);
            }
            _serviceProvider = services.BuildServiceProvider();
        }
    }

    /// <summary>
    /// Resolves a service from the initialized container.
    /// </summary>
    public static T GetRequiredService<T>() where T : notnull
    {
        return (_serviceProvider ?? throw new InvalidOperationException(
            "AzclipsServiceProvider has not been initialized. Call Initialize() first."))
            .GetRequiredService<T>();
    }

    /// <summary>
    /// Resolves an optional service from the initialized container.
    /// </summary>
    public static T? GetService<T>() where T : class
    {
        return (_serviceProvider ?? throw new InvalidOperationException(
            "AzclipsServiceProvider has not been initialized. Call Initialize() first."))
            .GetService<T>();
    }

    /// <summary>
    /// Resolves an optional keyed service from the initialized container.
    /// </summary>
    public static T? GetKeyedService<T>(object key) where T : class
    {
        return (_serviceProvider ?? throw new InvalidOperationException(
            "AzclipsServiceProvider has not been initialized. Call Initialize() first."))
            .GetKeyedService<T>(key);
    }

    /// <summary>
    /// Resolves a service from the initialized container by Type.
    /// Use this overload from PowerShell, which cannot call generic methods directly.
    /// </summary>
    public static object GetRequiredService(Type serviceType)
    {
        return (_serviceProvider ?? throw new InvalidOperationException(
            "AzclipsServiceProvider has not been initialized. Call Initialize() first."))
            .GetRequiredService(serviceType);
    }

    /// <summary>
    /// Disposes the initialized DI container, clears the cached provider,
    /// and removes all prepend/override registrations.
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
            _prependRegistrations.Clear();
            _overrideRegistrations.Clear();
            _serviceProvider = null;
        }
    }
}
