// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Core.Authentication;

public static class DIRegisterExtensions
{
    /// <summary>
    /// Extension method to register authentication-related services.
    /// </summary>
    public static IServiceCollection RegisterAuthenticationServices(this IServiceCollection services)
    {
        // TryAdd so a host that registered its own options first (benchmarks) keeps them.
        services.AddSingleton(new MsalClientOptions());
        services.AddSingleton<AzureCredentialFactory>();
        services.AddSingleton<ManagedIdentityAuthenticator>();
        services.AddSingleton<IAuthService, MsalAuthService>();
        services.AddSingleton<ISubscriptionService, SubscriptionService>();
        return services;
    }
}
