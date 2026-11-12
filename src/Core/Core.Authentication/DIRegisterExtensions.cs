// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Authentication.Recovery;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Authentication;

public static class DIRegisterExtensions
{
    /// <summary>
    /// Extension method to register authentication-related services.
    /// </summary>
    public static IServiceCollection RegisterAuthenticationServices(this IServiceCollection services)
    {
        services.AddSingleton(provider => new MsalClientOptions(provider.GetRequiredService<IPersistenceStore>()));
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
