// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Core.Engine;

public static class DIRegisterExtensions
{
    private const string SensitiveClientName = "Azclips.Sensitive";

    /// <summary>
    /// Extension method to register engine-related services.
    /// </summary>
    public static IServiceCollection RegisterEngineServices(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddHttpClient(SensitiveClientName).RemoveAllLoggers();
        services.PostConfigure<HttpClientFactoryOptions>(SensitiveClientName, options =>
            options.HttpMessageHandlerBuilderActions.Add(builder =>
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
            }));
        services.AddSingleton(provider => provider.GetRequiredService<IHttpClientFactory>().CreateClient(Options.DefaultName));
        services.AddSingleton(provider =>
        {
            var handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(SensitiveClientName);
            var client = new SensitiveHttpClient(handler, disposeHandler: false);
            try
            {
                var options = provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>().Get(SensitiveClientName);
                foreach (var configure in options.HttpClientActions) configure(client);
                return client;
            }
            catch
            {
                client.Dispose();
                throw;
            }
        });
        services.AddSingleton<IEngine, Engine>();

        return services;
    }
}
