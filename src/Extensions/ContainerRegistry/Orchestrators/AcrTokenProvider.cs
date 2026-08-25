// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Orchestrators;

/// <summary>
/// ACR token acquisition orchestrator. Handles the 3-step token exchange:
/// <list type="bullet">
///   <item>Step 1: Acquire AAD token via default auth delegate.</item>
///   <item>Step 2: Exchange AAD token for ACR refresh token (POST /oauth2/exchange).</item>
///   <item>Step 3: Exchange ACR refresh token for ACR access token (POST /oauth2/token).</item>
/// </list>
/// Token exchange requests use a raw <see cref="HttpClient"/> to avoid the pipeline
/// injecting an ARM Bearer token, which ACR's oauth endpoints would reject.
/// </summary>
internal sealed class AcrTokenProvider
{
    /// <summary>ACR login server suffix.</summary>
    internal const string AcrSuffix = ".azurecr.io";

    /// <summary>Resolves a registry name to its login server FQDN.</summary>
    internal static string ResolveLoginServer(string registryName) => $"{registryName}{AcrSuffix}";

    /// <summary>
    /// Acquires ACR token and wires <see cref="IAuthHandler.AcquireTokenAsyncOverride"/>
    /// so subsequent pipeline calls carry the ACR Bearer token.
    /// Call this before sending data-plane requests through the pipeline.
    /// </summary>
    internal static async Task SetupAuthAsync(IAuthHandler authHandler, JsonSerializerOptions jsonOptions, string loginServer, string scope)
    {
        var orchestrator = new AcrTokenProvider(authHandler, jsonOptions);
        string acrToken = await orchestrator.AcquireAcrAccessTokenAsync(loginServer, scope);
        authHandler.AcquireTokenAsyncOverride = (scopes, tenantId, promptOnError, ct, profile) =>
            Task.FromResult(new AuthResult
            {
                AccessToken = acrToken,
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                Scopes = Array.Empty<string>(),
                TenantId = tenantId ?? string.Empty,
            });
        authHandler.ApplyOverride = true;
    }

    private static readonly HttpClient s_httpClient = new();

    private readonly IAuthHandler _authHandler = null!;
    private readonly JsonSerializerOptions _jsonOptions = null!;

    /// <summary>Cached ACR access token, keyed by (loginServer, scope).</summary>
    private string? _cachedToken;
    private string? _cachedLoginServer;
    private string? _cachedScope;

    public AcrTokenProvider(
        IAuthHandler authHandler,
        JsonSerializerOptions jsonOptions)
    {
        _authHandler = authHandler;
        _jsonOptions = jsonOptions;
    }

    /// <summary>
    /// Acquires an ACR access token for the given login server and scope.
    /// Flow: AAD token → ACR refresh token → ACR access token.
    /// Caches the result for subsequent data-plane calls.
    /// </summary>
    public async Task<string> AcquireAcrAccessTokenAsync(string loginServer, string scope)
    {
        // Return cached token if same scope
        if (_cachedToken is not null &&
            string.Equals(_cachedLoginServer, loginServer, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_cachedScope, scope, StringComparison.Ordinal))
        {
            return _cachedToken;
        }

        // Step 1: Get AAD token via the default auth delegate
        var authResult = await _authHandler.AcquireTokenAsyncDefault(
            scopes: ["https://management.azure.com/.default"],
            tenantId: _authHandler.Profile?.TenantId,
            profile: _authHandler.Profile);

        string aadToken = authResult.AccessToken;

        // Step 2: Exchange AAD token for ACR refresh token
        string refreshToken = await ExchangeForRefreshTokenAsync(loginServer, aadToken);

        // Step 3: Exchange refresh token for ACR access token with scope
        // If scope is empty, use refresh token directly (for docker login)
        string accessToken = string.IsNullOrEmpty(scope)
            ? refreshToken
            : await ExchangeForAccessTokenAsync(loginServer, refreshToken, scope);

        // Cache
        _cachedToken = accessToken;
        _cachedLoginServer = loginServer;
        _cachedScope = scope;

        return accessToken;
    }

    /// <summary>
    /// Acquires only the ACR refresh token (Steps 1-2 of the full flow).
    /// Used by ConnectAcrProcessor for docker login (refresh token = password).
    /// </summary>
    public async Task<string> AcquireAcrRefreshTokenAsync(string loginServer)
    {
        // Step 1: Get AAD token via the default auth delegate
        var authResult = await _authHandler.AcquireTokenAsyncDefault(
            scopes: ["https://management.azure.com/.default"],
            tenantId: _authHandler.Profile?.TenantId,
            profile: _authHandler.Profile);

        string aadToken = authResult.AccessToken;

        // Step 2: Exchange AAD token for ACR refresh token
        return await ExchangeForRefreshTokenAsync(loginServer, aadToken);
    }

    private async Task<string> ExchangeForRefreshTokenAsync(string loginServer, string aadToken)
    {
        string url = $"https://{loginServer}/oauth2/exchange";
        string tenantId = _authHandler.Profile?.TenantId ?? "";
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "access_token",
            ["service"] = loginServer,
            ["tenant"] = tenantId,
            ["access_token"] = aadToken
        });

        using var response = await s_httpClient.PostAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"ACR token exchange failed ({response.StatusCode}): {errorBody}");
        }

        string body = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<AcrTokenResponse>(body, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to exchange AAD token for ACR refresh token.");

        return tokenResponse.Refresh_token
            ?? throw new InvalidOperationException("ACR refresh token was null in exchange response.");
    }

    private async Task<string> ExchangeForAccessTokenAsync(string loginServer, string refreshToken, string scope)
    {
        string url = $"https://{loginServer}/oauth2/token";
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["service"] = loginServer,
            ["scope"] = scope,
            ["refresh_token"] = refreshToken
        });

        using var response = await s_httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<AcrTokenResponse>(body, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to exchange refresh token for ACR access token.");

        return tokenResponse.Access_token
            ?? throw new InvalidOperationException("ACR access token was null in exchange response.");
    }
}
