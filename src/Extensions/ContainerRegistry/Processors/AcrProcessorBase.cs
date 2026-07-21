// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// Base class for ACR data-plane processors. Handles:
/// 1. Resolving the login server from registry name.
/// 2. OAuth2 token exchange (AAD token → ACR refresh token → ACR access token).
/// 3. Sending authenticated requests to the ACR data-plane.
/// </summary>
public abstract class AcrProcessorBase : IProcessor
{
    protected IRuntimeHttpPipeline HttpPipeline = null!;
    protected IEventPublisher Publisher = null!;
    protected IAuthHandler AuthHandler = null!;
    protected JsonSerializerOptions JsonOptions = null!;
    protected OutputStreamDelegate WriteOutput = null!;
    private static readonly HttpClient AcrHttpClient = new();

    public CancellationToken Token => CancellationToken.None;
    public Action Cancel => throw new NotImplementedException();

    public IProcessor Init(ProcessorContext context)
    {
        HttpPipeline = context.HttpPipeline;
        Publisher = context.Publisher;
        AuthHandler = context.AuthHandler;
        WriteOutput = context.OutputHandler.WriteOutput;
        JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return this;
    }

    public virtual Task ProcessAsync() => Task.CompletedTask;

    /// <summary>
    /// Resolves the login server FQDN from a registry name (e.g., "myregistry" → "myregistry.azurecr.io").
    /// </summary>
    protected static string ResolveLoginServer(string registryName)
    {
        if (registryName.Contains('.'))
        {
            // Already a FQDN
            return registryName;
        }
        return $"{registryName}.azurecr.io";
    }

    /// <summary>
    /// Acquires an ACR access token for the given scope by exchanging the AAD token.
    /// Flow: AAD token → ACR refresh token → ACR access token.
    /// </summary>
    protected async Task<string> AcquireAcrAccessTokenAsync(string loginServer, string scope)
    {
        // Step 1: Get AAD token for the ACR resource
        string acrResource = $"https://{loginServer}";
        var authResult = await AuthHandler.AcquireTokenAsyncDefault(
            scopes: [$"https://management.azure.com/.default"],
            tenantId: null);

        string aadToken = authResult.AccessToken;

        // Step 2: Exchange AAD token for ACR refresh token
        string refreshToken = await ExchangeForRefreshTokenAsync(loginServer, aadToken);

        // Step 3: Exchange refresh token for ACR access token with scope
        string accessToken = await ExchangeForAccessTokenAsync(loginServer, refreshToken, scope);

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
        using var response = await AcrHttpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"ACR token exchange failed ({response.StatusCode}): {errorBody}");
        }

        string body = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<AcrTokenResponse>(body, JsonOptions)
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
        using var response = await AcrHttpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<AcrTokenResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Failed to exchange refresh token for ACR access token.");

        return tokenResponse.Access_token
            ?? throw new InvalidOperationException("ACR access token was null in exchange response.");
    }

    /// <summary>
    /// Sends an authenticated request to the ACR data-plane API.
    /// </summary>
    protected async Task<HttpResponseMessage> SendAcrRequestAsync(HttpMethod method, string loginServer, string path, string scope, HttpContent? body = null)
    {
        string accessToken = await AcquireAcrAccessTokenAsync(loginServer, scope);
        string url = path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? path
            : $"https://{loginServer}{path}";

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = body;
        }

        var response = await AcrHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            response.Dispose();
            throw;
        }

        return response;
    }

    /// <summary>
    /// Sends a paginated GET request, following the <c>Link</c> header until all pages are fetched.
    /// Returns all raw JSON response bodies concatenated into a list.
    /// </summary>
    protected async Task<List<string>> SendAcrPagedGetAsync(string loginServer, string path, string scope)
    {
        var pages = new List<string>();
        string? currentPath = path;

        while (currentPath is not null)
        {
            using HttpResponseMessage response = await SendAcrRequestAsync(HttpMethod.Get, loginServer, currentPath, scope);

            if (response.Content is not null)
            {
                string body = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    pages.Add(body);
                }
            }

            // Parse Link header for next page: </acr/v1/_catalog?last=foo&n=100>; rel="next"
            currentPath = GetNextLinkPath(response);
        }

        return pages;
    }

    /// <summary>
    /// Extracts the next page path from the HTTP Link header.
    /// Format: &lt;/acr/v1/_catalog?last=foo&amp;n=100&gt;; rel="next"
    /// </summary>
    private static string? GetNextLinkPath(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var linkValues))
        {
            return null;
        }

        string? linkHeader = linkValues.FirstOrDefault();
        if (string.IsNullOrEmpty(linkHeader))
        {
            return null;
        }

        // Extract URL between < and >
        int start = linkHeader.IndexOf('<');
        int end = linkHeader.IndexOf('>');
        if (start < 0 || end < 0 || end <= start)
        {
            return null;
        }

        return linkHeader.Substring(start + 1, end - start - 1);
    }

    protected void EnsureRuntimeContext()
    {
        if (HttpPipeline is null)
            throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(HttpPipeline)}.");
        if (Publisher is null)
            throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(Publisher)}.");
        if (WriteOutput is null)
            throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(WriteOutput)}.");
    }
}

/// <summary>
/// Generic ACR processor base with typed deserialization.
/// </summary>
public abstract class AcrProcessorBase<TResponse> : AcrProcessorBase where TResponse : class
{
    /// <summary>Registry name (short name or FQDN).</summary>
    [ArgumentInfo(Required = true, Description = "The name of the container registry.", SerializedName = "RegistryName")]
    public string RegistryName { get; set; } = "";

    /// <summary>
    /// Derived processors implement this to define the HTTP method, path, scope, and optional body.
    /// </summary>
    protected abstract Task<(HttpMethod Method, string Path, string Scope, HttpContent? Body)> CreateAcrRequest();

    public override async Task ProcessAsync()
    {
        EnsureRuntimeContext();
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            string loginServer = ResolveLoginServer(RegistryName);
            var (method, path, scope, body) = await CreateAcrRequest();

            using HttpResponseMessage response = await SendAcrRequestAsync(method, loginServer, path, scope, body);

            if (response.Content is null)
            {
                return;
            }

            string rawBody = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return;
            }

            TResponse? result = JsonSerializer.Deserialize<TResponse>(rawBody, JsonOptions);
            if (result is not null)
            {
                await WriteOutput(result);
            }
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
