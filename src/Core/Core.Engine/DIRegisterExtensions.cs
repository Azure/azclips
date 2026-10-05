// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime.Handler;
using Core.Runtime.Handler;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Engine;

public static class DIRegisterExtensions
{
    /// <summary>
    /// Extension method to register engine-related services.
    /// </summary>
    public static IServiceCollection RegisterEngineServices(this IServiceCollection services)
    {
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IHttpDiagnosticsHandler>(_ => new HttpDiagnosticsHandler(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })));
        services.AddSingleton<IEngine, Engine>();

        return services;
    }
}
