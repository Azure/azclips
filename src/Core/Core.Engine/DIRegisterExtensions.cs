// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Engine;

public static class DIRegisterExtensions
{
    /// <summary>
    /// Extension method to register engine-related services.
    /// </summary>
    public static IServiceCollection RegisterEngineServices(this IServiceCollection services)
    {
        services.AddHttpClient(string.Empty)
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            });

        services.AddHttpClient(HttpClientTransportFactory.SensitiveClientName)
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            })
            .RemoveAllLoggers();

        services.AddSingleton(provider => provider.GetRequiredService<IHttpClientFactory>().CreateClient(string.Empty));
        services.AddSingleton<ISendAsyncTerminalFactory, HttpClientTransportFactory>();
        services.AddSingleton<IEngine, Engine>();

        return services;
    }
}
