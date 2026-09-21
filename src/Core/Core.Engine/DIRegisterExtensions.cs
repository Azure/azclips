// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
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
        services.AddSingleton<HttpClient>();
        services.AddSingleton(_ => new SensitiveHttpClient(new HttpClientHandler { AllowAutoRedirect = false }));
        services.AddSingleton<IEngine, Engine>();

        return services;
    }
}
