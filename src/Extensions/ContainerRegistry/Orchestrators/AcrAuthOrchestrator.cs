// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Orchestrators;

/// <summary>
/// ACR per-request auth provider. Implements <see cref="IRequestAuthProvider"/> to control
/// how each outgoing HTTP request is authenticated:
/// <list type="bullet">
///   <item>Token exchange requests (<c>/oauth2/exchange</c>, <c>/oauth2/token</c>): skip pipeline auth.</item>
///   <item>Data-plane requests to the login server: use the ACR access token.</item>
///   <item>All other requests: fall through to default (ARM) auth.</item>
/// </list>
/// </summary>
internal sealed class AcrAuthOrchestrator : IRequestAuthProvider
{
    private readonly IAuthHandler _authHandler;
    private readonly IRuntimeHttpPipeline _pipeline;
    private readonly IEventPublisher _publisher;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>Cached ACR access token, keyed by (loginServer, scope).</summary>
    private string? _cachedToken;
    private string? _cachedLoginServer;
    private string? _cachedScope;

    public AcrAuthOrchestrator(
        IAuthHandler authHandler,
        IRuntimeHttpPipeline pipeline,
        IEventPublisher publisher,
        JsonSerializerOptions jsonOptions)
    {
        _authHandler = authHandler;
        _pipeline = pipeline;
        _publisher = publisher;
        _jsonOptions = jsonOptions;
    }

    /// <inheritdoc />
    public Task<RequestAuthResult> GetAuthAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var uri = request.RequestUri;
        if (uri is null)
            return Task.FromResult(RequestAuthResult.Default);

        string path = uri.AbsolutePath;

        // Token exchange endpoints: skip auth so the pipeline doesn't inject a Bearer header
        if (path.EndsWith("/oauth2/exchange", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/oauth2/token", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(RequestAuthResult.Skip);
        }

        // Data-plane requests: use cached ACR token if available
        if (_cachedToken is not null &&
            _cachedLoginServer is not null &&
            uri.Host.Equals(_cachedLoginServer, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(RequestAuthResult.WithToken(_cachedToken));
        }

        // Anything else (e.g. ARM calls) uses default auth
        return Task.FromResult(RequestAuthResult.Default);
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
            tenantId: null);

        string aadToken = authResult.AccessToken;

        // Step 2: Exchange AAD token for ACR refresh token
        string refreshToken = await ExchangeForRefreshTokenAsync(loginServer, aadToken);

        // Step 3: Exchange refresh token for ACR access token with scope
        string accessToken = await ExchangeForAccessTokenAsync(loginServer, refreshToken, scope);

        // Cache
        _cachedToken = accessToken;
        _cachedLoginServer = loginServer;
        _cachedScope = scope;

        return accessToken;
    }

    private async Task<string> ExchangeForRefreshTokenAsync(string loginServer, string aadToken)
    {
        string url = $"https://{loginServer}/oauth2/exchange";
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "access_token",
            ["service"] = loginServer,
            ["access_token"] = aadToken
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        using var response = await _pipeline.SendAsync(request, _publisher);

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

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        using var response = await _pipeline.SendAsync(request, _publisher);
        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<AcrTokenResponse>(body, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to exchange refresh token for ACR access token.");

        return tokenResponse.Access_token
            ?? throw new InvalidOperationException("ACR access token was null in exchange response.");
    }
}
