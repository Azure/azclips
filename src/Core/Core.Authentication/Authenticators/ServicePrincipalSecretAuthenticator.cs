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
        if (request.Credential is not AuthCredential.ClientSecret credential)
            throw AzclipsErrorException.InvalidInput(
                $"Expected the credential to be a ClientSecret, but got {request.Credential?.GetType().Name ?? "null"}");

        using var _ = TelemetryService.StartActivity($"{nameof(ServicePrincipalSecretAuthenticator)}.{nameof(AuthenticateAsync)}");

        string clientId = Require(credential.ClientId, "client ID");
        string tenantId = Require(request.TenantId, "tenant ID");
        string clientSecret = Require(credential.Secret, "client secret");
        string[] scopes = request.Scopes ?? [ArmDefaultScope];

        Logger.Debug($"[{nameof(ServicePrincipalSecretAuthenticator)}] ClientId:'{clientId}', TenantId:'{tenantId}'");

        // Forced past the cache: the app cache is keyed by client and scope, not by assertion, so a
        // token left by an earlier sign-in would otherwise satisfy this call and the new assertion
        // would be saved without ever being presented to Entra.
        var result = await AcquireAsync(
            clientId, clientSecret, tenantId, scopes, forceRefresh: true, cancellationToken);

        // Saved only after Entra accepted it, so a rejected token never displaces a working one.
        await ServicePrincipalSecretStore.SaveClientSecretAsync(
            clientId, clientSecret, Options.CacheDirectory, Logger);

        return result;
    }

    public async Task<AuthResult> AcquireTokenAsync(Profile profile, string[]? scopes, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(ServicePrincipalSecretAuthenticator)}.{nameof(AcquireTokenAsync)}");

        var clientId = Require(profile.ClientId, "client ID");
        var tenantId = Require(profile.TenantId, "tenant ID");

        if (await ServicePrincipalSecretStore.GetClientSecretAsync(clientId, Options.CacheDirectory, Logger) is not string clientSecret || string.IsNullOrEmpty(clientSecret))
            throw new InvalidOperationException("Client secret not found in the secret store.");

        return await AcquireAsync(
            clientId,
            clientSecret,
            tenantId,
            scopes ?? [ArmDefaultScope],
            // Reusing a cached token is the whole point of a silent refresh.
            forceRefresh: false,
            cancellationToken);
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
