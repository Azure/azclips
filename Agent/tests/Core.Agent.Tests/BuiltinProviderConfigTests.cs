// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Agent.Providers;
using Core.Authentication;
using Xunit;

namespace Core.Agent.Tests;

public class BuiltinProviderConfigTests
{
    private const string ApiResource = "api://a5ede409-60d3-4a6c-93e6-eb2e7271e8e3";
    private const string ApiScope = ApiResource + "/.default";

    [Fact]
    public void DefaultScope_TargetsIntelligentApi()
    {
        Assert.Equal(ApiScope, new AgentOptions().Builtin.Scope);
    }

    [Fact]
    public async Task Build_RequestsOnlyApiScope_AndPreservesProviderSettings()
    {
        var token = Token();
        var auth = new RecordingAuthService(() => Task.FromResult(token));
        var options = new BuiltinProviderOptions();
        using var cancellation = new CancellationTokenSource();

        var provider = await BuiltinProviderConfig.BuildAsync(options, auth, cancellation.Token);

        Assert.NotNull(auth.RequestedScopes);
        Assert.Equal([ApiScope], auth.RequestedScopes);
        Assert.Equal(1, auth.Calls);
        Assert.Null(auth.TenantId);
        Assert.False(auth.PromptOnError);
        Assert.Equal(cancellation.Token, auth.CancellationToken);
        Assert.Equal("azure", provider.Type);
        Assert.Equal("responses", provider.WireApi);
        Assert.Equal(options.Endpoint, provider.BaseUrl);
        Assert.Equal(options.ModelId, provider.ModelId);
        Assert.Equal(options.ModelId, provider.WireModel);
        Assert.Equal(options.ApiVersion, provider.Azure?.ApiVersion);
        Assert.Equal(token.AccessToken, provider.BearerToken);
    }

    [Fact]
    public async Task Build_PreservesExplicitScopeOverride()
    {
        var auth = new RecordingAuthService(() => Task.FromResult(Token()));
        var options = new BuiltinProviderOptions { Scope = "api://test-resource/custom.permission" };

        await BuiltinProviderConfig.BuildAsync(options, auth, CancellationToken.None);

        Assert.NotNull(auth.RequestedScopes);
        Assert.Equal([options.Scope], auth.RequestedScopes);
        Assert.Equal(1, auth.Calls);
    }

    [Fact]
    public async Task Build_DefaultScope_ResolvesToTheManagedIdentityApiResource()
    {
        var auth = new RecordingAuthService(() => Task.FromResult(Token()));

        await BuiltinProviderConfig.BuildAsync(new(), auth, CancellationToken.None);

        Assert.NotNull(auth.RequestedScopes);
        Assert.Equal(ApiResource, ManagedIdentityCredential.ScopeToResource(auth.RequestedScopes));
    }

    [Fact]
    public async Task Build_ReportsAuthenticationFailure_WithoutRetryOrFallback()
    {
        var failure = new InvalidOperationException("Test authentication failure");
        var auth = new RecordingAuthService(() => Task.FromException<AuthResult>(failure));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => BuiltinProviderConfig.BuildAsync(new(), auth, CancellationToken.None));

        Assert.Same(failure, error.InnerException);
        Assert.NotNull(auth.RequestedScopes);
        Assert.Equal([ApiScope], auth.RequestedScopes);
        Assert.Equal(1, auth.Calls);
    }

    [Fact]
    public async Task Build_PropagatesCancellation_WithoutRetryOrFallback()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var auth = new RecordingAuthService(() => Task.FromCanceled<AuthResult>(cancellation.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => BuiltinProviderConfig.BuildAsync(new(), auth, cancellation.Token));

        Assert.NotNull(auth.RequestedScopes);
        Assert.Equal([ApiScope], auth.RequestedScopes);
        Assert.Equal(1, auth.Calls);
    }

    [Fact]
    public async Task Build_RejectsEmptyToken_WithoutRetryOrFallback()
    {
        var auth = new RecordingAuthService(() => Task.FromResult(Token() with { AccessToken = "" }));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => BuiltinProviderConfig.BuildAsync(new(), auth, CancellationToken.None));

        Assert.Equal(1, auth.Calls);
    }

    private static AuthResult Token() => new()
    {
        AccessToken = "not-a-real-token",
        ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(5),
        Scopes = [ApiScope],
        TenantId = "00000000-0000-0000-0000-000000000001",
    };

    private sealed class RecordingAuthService(Func<Task<AuthResult>> acquire) : IAuthService
    {
        public int Calls { get; private set; }
        public string[]? RequestedScopes { get; private set; }
        public string? TenantId { get; private set; }
        public bool PromptOnError { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<AuthResult> AcquireTokenSilentlyAsync(
            string[]? scopes, string? tenantId, bool promptOnError = false,
            CancellationToken cancellationToken = default, Profile? profile = null)
        {
            Calls++;
            RequestedScopes = scopes;
            TenantId = tenantId;
            PromptOnError = promptOnError;
            CancellationToken = cancellationToken;
            return acquire();
        }

        public Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task LogoutAsync(string? userPrincipalName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> CanAcquireTokenSilentlyAsync(
            string? tenantId, Profile profile, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
