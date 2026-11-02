// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions;
using Core.Authentication;
using Microsoft.Identity.Client;
using Moq;

namespace Cli.Tests;

[Collection("Azclips Service Provider Tests")]
public sealed class ServicePrincipalSecretTokenTests : IDisposable
{
    private const string ClientId = "aaaaaaaa-1111-1111-1111-111111111111";
    private const string TenantId = "bbbbbbbb-2222-2222-2222-222222222222";
    private const string AcceptedSecret = "unit-test-accepted-secret";
    private const string ReplacementSecret = "unit-test-replacement-secret";
    private const string Authority = "https://login.microsoftonline.com";
    private const string TokenEndpoint = $"{Authority}/{TenantId}/oauth2/v2.0/token";
    private readonly string _directory = Directory.CreateTempSubdirectory("azclips-sp-tokens-").FullName;
    private readonly ServicePrincipalSecretStore _secrets = new();
    private readonly ILoggingService _logger = Mock.Of<ILoggingService>();
    private readonly ITelemetryService _telemetry = Mock.Of<ITelemetryService>();
    private readonly TokenHttpClientFactory _http = new();

    public ServicePrincipalSecretTokenTests()
    {
        MsalTokenCacheFactory.ForcePlaintextForTests = true;
        _http.ReadStoredSecret = () => _secrets.GetClientSecretAsync(ClientId, _directory, _logger);
    }

    private MsalClientOptions Options => new() { CacheDirectory = _directory, HttpClientFactory = _http };

    private MsalAuthService CreateService()
    {
        var options = Options;
        return new(
            _logger, _telemetry, Mock.Of<IProfileService>(), new ClientMetadata(ClientType.CLI, ClientId),
            new ManagedIdentityAuthenticator(new AzureCredentialFactory(), _logger, _telemetry),
            new FederatedTokenAuthenticator(_logger, _telemetry, _secrets, options),
            new ServicePrincipalSecretAuthenticator(_logger, _telemetry, _secrets, options),
            _secrets, options);
    }

    private static LoginRequest Request(string secret) =>
        new(TenantId, null, new AuthCredential.ClientSecret(ClientId, secret));

    private Task<string?> ReadSecretAsync() => _secrets.GetClientSecretAsync(ClientId, _directory, _logger);

    [Fact]
    public async Task LoginAsync_AcceptedSecret_IsSavedOnlyAfterTokenExchange()
    {
        var result = await CreateService().LoginAsync(Request(AcceptedSecret));

        var exchange = Assert.Single(_http.Exchanges);
        Assert.Equal(ClientId, exchange.ClientId);
        Assert.Equal(AcceptedSecret, exchange.Secret);
        Assert.Null(exchange.PreviousSecret);
        Assert.Equal(AcceptedSecret, await ReadSecretAsync());
        Assert.Equal("offline-access-token-1", result.AccessToken);
        Assert.Equal(TenantId, result.TenantId);
        Assert.Equal(ClientId, result.UserPrincipalName);
        Assert.Contains(AuthConstants.ArmDefaultScope, result.Scopes);
        Assert.True(result.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(30));
    }

    [Fact]
    public async Task LoginAsync_WithCachedToken_ExchangesAndSavesReplacementSecret()
    {
        await CreateService().LoginAsync(Request(AcceptedSecret));

        var result = await CreateService().LoginAsync(Request(ReplacementSecret));

        Assert.Equal(2, _http.Exchanges.Count);
        var exchange = _http.Exchanges[1];
        Assert.Equal(ReplacementSecret, exchange.Secret);
        Assert.Equal(AcceptedSecret, exchange.PreviousSecret);
        Assert.Equal("offline-access-token-2", result.AccessToken);
        Assert.Equal(ReplacementSecret, await ReadSecretAsync());
    }

    [Fact]
    public async Task LoginAsync_RejectedSecretWithCachedToken_PreservesPreviousSecret()
    {
        await CreateService().LoginAsync(Request(AcceptedSecret));
        _http.RejectSecret = true;

        var error = await Assert.ThrowsAsync<MsalServiceException>(
            () => CreateService().LoginAsync(Request(ReplacementSecret)));

        Assert.Equal("invalid_client", error.ErrorCode);
        Assert.Equal(2, _http.Exchanges.Count);
        Assert.Equal(ReplacementSecret, _http.Exchanges[1].Secret);
        Assert.Equal(AcceptedSecret, _http.Exchanges[1].PreviousSecret);
        Assert.Equal(AcceptedSecret, await ReadSecretAsync());
    }

    [Fact]
    public async Task AcquireTokenSilentlyAsync_WithCachedToken_ReusesCacheWithoutHttp()
    {
        var login = await CreateService().LoginAsync(Request(AcceptedSecret));
        int requestCount = _http.RequestCount;
        _http.BlockRequests = true;
        var profile = new Profile
        {
            Name = "offline-service-principal",
            AuthMethod = AuthMethod.ServicePrincipalSecret,
            ClientId = ClientId,
            TenantId = TenantId,
        };

        // A new service builds a new MSAL client, so this also exercises the persisted app cache.
        var result = await CreateService().AcquireTokenSilentlyAsync(null, null, profile: profile);

        Assert.Equal(login.AccessToken, result.AccessToken);
        Assert.Equal(login.ExpiresOn.ToUnixTimeSeconds(), result.ExpiresOn.ToUnixTimeSeconds());
        Assert.Equal(requestCount, _http.RequestCount);
        Assert.Single(_http.Exchanges);
        Assert.Equal(AcceptedSecret, await ReadSecretAsync());
    }

    public void Dispose()
    {
        _http.Dispose();
        MsalTokenCacheFactory.ForcePlaintextForTests = false;
        Directory.Delete(_directory, recursive: true);
    }

    private sealed record TokenExchange(string ClientId, string Secret, string? PreviousSecret);

    private sealed class TokenHttpClientFactory : IMsalHttpClientFactory, IDisposable
    {
        private readonly HttpClient _client;
        public TokenHttpClientFactory() => _client = new HttpClient(new Handler(this));
        public Func<Task<string?>> ReadStoredSecret { get; set; } =
            () => throw new InvalidOperationException("The secret-store observer must be configured.");
        public List<TokenExchange> Exchanges { get; } = [];
        public int RequestCount { get; private set; }
        public bool RejectSecret { get; set; }
        public bool BlockRequests { get; set; }
        public HttpClient GetHttpClient() => _client;
        public void Dispose() => _client.Dispose();

        private sealed class Handler(TokenHttpClientFactory owner) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                owner.RequestCount++;
                if (owner.BlockRequests)
                    throw new InvalidOperationException("Cached acquisition must not make HTTP requests.");

                if (request.Method == HttpMethod.Get &&
                    request.RequestUri?.AbsoluteUri == $"{Authority}/{TenantId}/v2.0/.well-known/openid-configuration")
                {
                    return Json(HttpStatusCode.OK, $$"""
                        {
                          "authorization_endpoint": "{{Authority}}/{{TenantId}}/oauth2/v2.0/authorize",
                          "token_endpoint": "{{TokenEndpoint}}",
                          "issuer": "{{Authority}}/{{TenantId}}/v2.0",
                          "jwks_uri": "{{Authority}}/{{TenantId}}/discovery/v2.0/keys"
                        }
                        """);
                }

                if (request.Method == HttpMethod.Get &&
                    request.RequestUri?.GetLeftPart(UriPartial.Path) == $"{Authority}/common/discovery/instance")
                {
                    return Json(HttpStatusCode.OK, $$"""
                        {
                          "tenant_discovery_endpoint": "{{Authority}}/{{TenantId}}/v2.0/.well-known/openid-configuration",
                          "metadata": [{
                            "preferred_network": "login.microsoftonline.com",
                            "preferred_cache": "login.windows.net",
                            "aliases": ["login.microsoftonline.com", "login.windows.net", "sts.windows.net"]
                          }]
                        }
                        """);
                }

                if (request.Method != HttpMethod.Post || request.RequestUri?.AbsoluteUri != TokenEndpoint)
                    throw new InvalidOperationException($"Unexpected MSAL request: {request.Method} {request.RequestUri}");

                string body = await (request.Content
                    ?? throw new InvalidOperationException("Token request has no content."))
                    .ReadAsStringAsync(cancellationToken);
                var fields = body.Split('&')
                    .Select(part => part.Split('=', 2))
                    .ToDictionary(part => Decode(part[0]), part => Decode(part[1]));
                Assert.Equal("client_credentials", fields["grant_type"]);
                Assert.Equal(AuthConstants.ArmDefaultScope, fields["scope"]);
                owner.Exchanges.Add(new(
                    fields["client_id"], fields["client_secret"], await owner.ReadStoredSecret()));

                if (owner.RejectSecret)
                    return Json(HttpStatusCode.BadRequest,
                        """{"error":"invalid_client","error_description":"The supplied test secret was rejected."}""");

                return Json(HttpStatusCode.OK, $$"""
                    {
                      "token_type": "Bearer",
                      "expires_in": 3600,
                      "ext_expires_in": 3600,
                      "access_token": "offline-access-token-{{owner.Exchanges.Count}}",
                      "scope": "{{AuthConstants.ArmDefaultScope}}"
                    }
                    """);
            }

            private static string Decode(string value) => Uri.UnescapeDataString(value.Replace("+", " "));

            private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
