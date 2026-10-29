// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Identity.Client;

namespace Core.Authentication;

internal class ServicePrincipalSecretAuthenticator(
    ILoggingService Logger,
    ITelemetryService TelemetryService,
    IServicePrincipalSecretStore ServicePrincipalSecretStore,
    MsalClientOptions Options)
{
    private const string ArmDefaultScope = AuthConstants.ArmDefaultScope;

    public async Task<AuthResult> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(ServicePrincipalSecretAuthenticator)}.{nameof(AuthenticateAsync)}");
        var result = await AcquireTokenAsync(request, forceRefresh: true, cancellationToken);

        // Saved only after Entra accepted it, so a rejected secret never displaces a working one.
        var credential = (AuthCredential.ClientSecret)request.Credential;
        await ServicePrincipalSecretStore.SaveClientSecretAsync(
            credential.ClientId, credential.Secret, Options.CacheDirectory, Logger);

        return result;
    }

    public Task<AuthResult> AcquireTokenAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
        AcquireTokenAsync(request, forceRefresh: false, cancellationToken);

    private async Task<AuthResult> AcquireTokenAsync(LoginRequest request, bool forceRefresh, CancellationToken cancellationToken)
    {
        if (request.Credential is not AuthCredential.ClientSecret credential)
            throw AzclipsErrorException.InvalidInput(
                $"Expected the credential to be a ClientSecret, but got {request.Credential?.GetType().Name ?? "null"}");

        using var _ = TelemetryService.StartActivity($"{nameof(ServicePrincipalSecretAuthenticator)}.{nameof(AcquireTokenAsync)}");

        string clientId = Require(credential.ClientId, "client ID");
        string tenantId = Require(request.TenantId, "tenant ID");
        string clientSecret = Require(credential.Secret, "client secret");
        string[] scopes = request.Scopes ?? [ArmDefaultScope];

        Logger.Debug($"[{nameof(ServicePrincipalSecretAuthenticator)}] ClientId:'{clientId}', TenantId:'{tenantId}'");

        // A fresh login must validate the supplied secret; silent acquisition may reuse the cache.
        return await AcquireAsync(
            clientId, clientSecret, tenantId, scopes, forceRefresh, cancellationToken);
    }

    private async Task<AuthResult> AcquireAsync(
        string clientId,
        string clientSecret,
        string tenantId,
        string[] scopes,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var app = ConfidentialClientApplicationBuilder.Create(clientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
            .WithClientSecret(clientSecret)
            .WithLogging(LogDebug)
            .Build();

        // All applications share one app cache; MSAL keys them by client id. Null when policy keeps
        // credentials in memory.
        var resolution = await MsalTokenCacheFactory.CreateAsync(
            prefix: MsalTokenCacheFactory.AppCachePrefix,
            cacheDirectory: Options.CacheDirectory,
            logger: Logger);
        resolution.Helper?.RegisterCache(app.AppTokenCache);

        var result = await app.AcquireTokenForClient(scopes)
            .WithForceRefresh(forceRefresh)
            .ExecuteAsync(cancellationToken);

        Logger.Debug($"[{nameof(ServicePrincipalSecretAuthenticator)}] Token acquired. ExpiresOn:{result.ExpiresOn}, TokenSource:{result.AuthenticationResultMetadata.TokenSource}");

        return new AuthResult
        {
            AccessToken = result.AccessToken,
            ExpiresOn = result.ExpiresOn,
            Scopes = [.. result.Scopes],
            TenantId = string.IsNullOrEmpty(result.TenantId) ? tenantId : result.TenantId,
            // Client credentials flows have no user; surface the app identity so profiles stay readable.
            UserPrincipalName = clientId,
            CorrelationId = result.CorrelationId,
        };
    }

    private void LogDebug(Microsoft.Identity.Client.LogLevel level, string message, bool containsPii)
    {
        Logger.Debug($"[{nameof(ServicePrincipalSecretAuthenticator)}] MSAL Log - Level:{level}, Message:{message}, ContainsPii:{containsPii}");
    }

    private static string Require(string? value, string description) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"A {description} is required for service principal login.")
            : value;
}
