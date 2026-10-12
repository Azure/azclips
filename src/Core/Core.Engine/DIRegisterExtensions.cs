// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Core.Engine;

public static class DIRegisterExtensions
{
    /// <summary>
    /// Extension method to register engine-related services.
    /// </summary>
    public static IServiceCollection RegisterEngineServices(this IServiceCollection services)
    {
        AddConfiguredClient(services, Options.DefaultName);
        AddSensitiveClient(services);
        services.AddSingleton(provider => provider.GetRequiredService<IHttpClientFactory>().CreateClient(Options.DefaultName));
        services.AddSingleton<ISendAsyncTerminalFactory, HttpClientTransportFactory>();
        services.AddSingleton<IEngine, Engine>();

        return services;
    }

    private static IHttpClientBuilder AddConfiguredClient(IServiceCollection services, string name) =>
        services.AddHttpClient(name)
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler((handler, _) =>
            {
                if (handler is SocketsHttpHandler sockets && sockets.PooledConnectionLifetime == Timeout.InfiniteTimeSpan)
                    sockets.PooledConnectionLifetime = TimeSpan.FromMinutes(2);
            });

    private static void AddSensitiveClient(IServiceCollection services)
    {
        AddConfiguredClient(services, HttpClientTransportFactory.SensitiveClientName).RemoveAllLoggers();
        services.AddKeyedSingleton<HttpClient>(HttpClientTransportFactory.SensitiveClientName,
            (provider, _) => provider.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientTransportFactory.SensitiveClientName));
        services.PostConfigure<HttpClientFactoryOptions>(HttpClientTransportFactory.SensitiveClientName, options =>
            options.HttpMessageHandlerBuilderActions.Add(DisableSensitiveRedirects));
    }

    private static void DisableSensitiveRedirects(HttpMessageHandlerBuilder builder)
    {
        HttpMessageHandler handler = builder.PrimaryHandler;
        while (handler is DelegatingHandler delegating)
            handler = delegating.InnerHandler ?? throw new InvalidOperationException("The sensitive HTTP transport has an incomplete handler chain.");
        switch (handler)
        {
            case HttpClientHandler primary:
                primary.AllowAutoRedirect = false;
                break;
            case SocketsHttpHandler primary:
                primary.AllowAutoRedirect = false;
                break;
            default:
                throw new InvalidOperationException("The sensitive HTTP transport must use HttpClientHandler or SocketsHttpHandler so redirects can be disabled.");
        }
    }
}
