// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Authentication.Recovery;
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
        services.AddSingleton(new MsalClientOptions());
        services.AddSingleton<AzureCredentialFactory>();
        services.AddSingleton<ManagedIdentityAuthenticator>();
        services.AddSingleton<FederatedTokenAuthenticator>();
        services.AddSingleton<ServicePrincipalSecretAuthenticator>();
        services.AddSingleton<IAuthService, MsalAuthService>();
        services.AddSingleton<ISubscriptionService, SubscriptionService>();
        services.AddSingleton<IFailureHistoryKeyStore, ProtectedFailureHistoryKeyStore>();
        services.AddSingleton<IServicePrincipalSecretStore, ServicePrincipalSecretStore>();
        return services;
    }
}
