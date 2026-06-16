// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Authentication;

public static class DIRegisterExtensions
{
    /// <summary>
    /// Extension method to register authentication-related services.
    /// </summary>
    public static IServiceCollection RegisterAuthenticationServices(this IServiceCollection services)
    {
        services.AddSingleton<AzureCredentialFactory>();
        services.AddSingleton<MsalTokenCacheProvider>();
        services.AddSingleton<ManagedIdentityAuthenticator>();
        services.AddSingleton<IAuthService, MsalAuthService>();
        services.AddSingleton<IAuthContextService, AuthContextService>();
        services.AddSingleton<ISubscriptionService, SubscriptionService>();
        return services;
    }
}