// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Identity.Client;

namespace Core.Authentication;

/// <summary>
/// Service principal login using a federated (OIDC) client assertion, as in workload identity
/// federation. Every sign-in presents a newly issued assertion; it is kept afterwards so a refresh for
/// another scope does not need the user again.
/// </summary>
public class FederatedTokenAuthenticator(
    ILoggingService Logger,
    ITelemetryService TelemetryService,
    MsalClientOptions Options)
{
    private const string ArmDefaultScope = AuthConstants.ArmDefaultScope;

    public async Task<AuthResult> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(FederatedTokenAuthenticator)}.{nameof(AuthenticateAsync)}");

        var clientId = Require(request.ServicePrincipalClientId, "client ID");
        var tenantId = Require(request.TenantId, "tenant ID");
        var scopes = request.Scopes ?? [ArmDefaultScope];

        Logger.Debug($"[{nameof(FederatedTokenAuthenticator)}] Acquiring token via federated credential. ClientId:'{clientId}', TenantId:'{tenantId}'");

        // Deliberately no fallback to the store: signing in must prove the caller holds a valid
        // assertion rather than replay a stored one.
        var assertion = request.FederatedToken?.Trim();

        if (string.IsNullOrWhiteSpace(assertion))
        {
            throw AzclipsErrorException.NotSignedIn("No federated token was provided.");
        }

        // Forced past the cache: the app cache is keyed by client and scope, not by assertion, so a
        // token left by an earlier sign-in would otherwise satisfy this call and the new assertion
        // would be saved without ever being presented to Entra.
        var result = await AcquireAsync(
            clientId, tenantId, scopes, _ => assertion, forceRefresh: true, cancellationToken);

        // Saved only after Entra accepted it, so a rejected token never displaces a working one.
        await ServicePrincipalSecretStore.SaveAssertionAsync(
            clientId, tenantId, assertion, Options.CacheDirectory, Logger);

        return result;
    }

    /// <summary>Re-acquires a token for the service principal saved on the profile.</summary>
    public async Task<AuthResult> AcquireTokenAsync(Profile profile, string[]? scopes, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(FederatedTokenAuthenticator)}.{nameof(AcquireTokenAsync)}");

        var clientId = Require(profile.ClientId, "client ID");
        var tenantId = Require(profile.TenantId, "tenant ID");

        var assertion = await ServicePrincipalSecretStore.TryGetAssertionAsync(
            clientId, tenantId, Options.CacheDirectory, Logger);

        if (assertion is null)
        {
            throw AzclipsErrorException.InvalidInput(
                $"Profile '{profile.Name}' signed in as a service principal, but no federated token is stored " +
                "for it. Sign in again with a newly issued token.");
        }

        return await AcquireAsync(
            clientId,
            tenantId,
            scopes ?? [ArmDefaultScope],
            _ => assertion,
            // Reusing a cached token is the whole point of a silent refresh.
            forceRefresh: false,
            cancellationToken);
    }

    private async Task<AuthResult> AcquireAsync(
        string clientId,
        string tenantId,
        string[] scopes,
        Func<AssertionRequestOptions, string> assertionFactory,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var app = ConfidentialClientApplicationBuilder.Create(clientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
            .WithClientAssertion(options => Task.FromResult(assertionFactory(options)))
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

        Logger.Debug($"[{nameof(FederatedTokenAuthenticator)}] Token acquired. ExpiresOn:{result.ExpiresOn}, TokenSource:{result.AuthenticationResultMetadata.TokenSource}");

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

    private static string Require(string? value, string description) =>
        string.IsNullOrWhiteSpace(value)
            ? throw AzclipsErrorException.InvalidInput($"A {description} is required for service principal login.")
            : value;
}
