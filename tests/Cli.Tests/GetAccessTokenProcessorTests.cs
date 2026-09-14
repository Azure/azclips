// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Accounts.Processors;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cli.Tests;

/// <summary>
/// Behavioral tests for <see cref="GetAccessTokenProcessor"/>: tenant-compatibility validation for
/// managed identity accounts, secure-by-default token rendering with per-surface plaintext opt-in
/// (CLI <c>--plaintext</c> flag; PowerShell env var), and scope resolution flowing through to the
/// auth service.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class GetAccessTokenProcessorTests
{
    private static Mock<IAuthService> AuthReturning(string token, Action<string[]?, string?>? capture = null)
    {
        var mock = new Mock<IAuthService>();
        mock.Setup(a => a.AcquireTokenSilentlyAsync(
                It.IsAny<string[]?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<Profile?>()))
            .Callback<string[]?, string?, bool, CancellationToken, Profile?>((s, t, _, _, _) => capture?.Invoke(s, t))
            .ReturnsAsync(new AuthResult
            {
                AccessToken = token,
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                Scopes = Array.Empty<string>(),
                TenantId = "tid",
            });
        return mock;
    }

    private static void InitDi(ClientType clientType, IAuthService auth)
    {
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services => services.AddSingleton(auth));
        AzclipsServiceProvider.Initialize(clientType);
    }

    private static Profile ProfileWith(AuthMethod method, string tenantId = "tid") => new()
    {
        Name = "p",
        TenantId = tenantId,
        SubscriptionId = "sub-1",
        CloudName = "AzureCloud",
        AuthMethod = method,
        UserPrincipalName = "user@contoso.com",
    };

    private static GetAccessTokenProcessor InitProcessor(GetAccessTokenProcessor processor, Profile? profile, out FakeOutputHandler output)
    {
        output = new FakeOutputHandler();
        var context = new ProcessorContext(
            new FakeHttpPipeline((_, _) => throw new NotSupportedException()),
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile);
        processor.Init(context);
        return processor;
    }

    private static async Task<object> RunAsync(GetAccessTokenProcessor processor, Profile profile)
    {
        InitProcessor(processor, profile, out var output);
        await processor.ProcessAsync();
        return Assert.Single(output.WrittenValues);
    }

    private static string Unwrap(System.Security.SecureString secure) =>
        new System.Net.NetworkCredential(string.Empty, secure).Password;

    [Fact]
    public async Task ProcessAsync_NullProfile_Throws()
    {
        InitDi(ClientType.CLI, AuthReturning("secret").Object);
        var processor = InitProcessor(new GetAccessTokenProcessor(), profile: null, out _);
        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());
    }

    [Fact]
    public async Task ProcessAsync_ManagedIdentity_MismatchedExplicitTenant_Throws()
    {
        InitDi(ClientType.CLI, AuthReturning("secret").Object);
        var processor = InitProcessor(
            new GetAccessTokenProcessor { Tenant = "other-tenant" },
            ProfileWith(AuthMethod.ManagedIdentity, tenantId: "mi-tenant"), out _);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Contains("not valid for the current account", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_ManagedIdentity_MatchingExplicitTenant_Succeeds()
    {
        InitDi(ClientType.CLI, AuthReturning("secret").Object);
        var result = await RunAsync(
            new GetAccessTokenProcessor { Tenant = "mi-tenant", Plaintext = true },
            ProfileWith(AuthMethod.ManagedIdentity, tenantId: "mi-tenant"));
        Assert.Equal("secret", Assert.IsType<AccessTokenResult>(result).Token);
    }

    [Fact]
    public async Task ProcessAsync_ManagedIdentity_ExplicitTenantWithUnknownProfileTenant_Throws()
    {
        InitDi(ClientType.CLI, AuthReturning("secret").Object);
        var processor = InitProcessor(
            new GetAccessTokenProcessor { Tenant = "requested-tenant" },
            ProfileWith(AuthMethod.ManagedIdentity, tenantId: ""), out _);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Contains("cannot be honored", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_BrowserAccount_ExplicitDifferentTenant_IsAllowed()
    {
        InitDi(ClientType.CLI, AuthReturning("secret").Object);
        var result = await RunAsync(
            new GetAccessTokenProcessor { Tenant = "other-tenant", Plaintext = true },
            ProfileWith(AuthMethod.Browser, tenantId: "home-tenant"));
        Assert.Equal("secret", Assert.IsType<AccessTokenResult>(result).Token);
    }

    [Fact]
    public async Task ProcessAsync_CliDefault_RedactsToken()
    {
        InitDi(ClientType.CLI, AuthReturning("secret").Object);
        var result = await RunAsync(new GetAccessTokenProcessor(), ProfileWith(AuthMethod.Browser));
        Assert.Equal(AccessTokenResult.RedactedPlaceholder, Assert.IsType<AccessTokenResult>(result).Token);
    }

    [Fact]
    public async Task ProcessAsync_CliPlaintextFlag_RevealsToken()
    {
        InitDi(ClientType.CLI, AuthReturning("secret").Object);
        var result = await RunAsync(new GetAccessTokenProcessor { Plaintext = true }, ProfileWith(AuthMethod.Browser));
        Assert.Equal("secret", Assert.IsType<AccessTokenResult>(result).Token);
    }

    [Fact]
    public async Task ProcessAsync_PowerShellPlaintextSwitch_RevealsPlainStringToken()
    {
        InitDi(ClientType.PowerShell, AuthReturning("secret").Object);
        var result = await RunAsync(new GetAccessTokenProcessor { Plaintext = true }, ProfileWith(AuthMethod.Browser));
        Assert.Equal("secret", Assert.IsType<AccessTokenResult>(result).Token);
    }

    [Fact]
    public async Task ProcessAsync_PowerShellDefault_ReturnsSecureStringToken()
    {
        InitDi(ClientType.PowerShell, AuthReturning("secret").Object);
        var result = await RunAsync(new GetAccessTokenProcessor(), ProfileWith(AuthMethod.Browser));
        var secure = Assert.IsType<SecureAccessTokenResult>(result);
        Assert.Equal("secret", Unwrap(secure.Token));
    }

    [Fact]
    public async Task ProcessAsync_PopulatesUnixTimeExpiry()
    {
        InitDi(ClientType.CLI, AuthReturning("secret").Object);
        var result = Assert.IsType<AccessTokenResult>(
            await RunAsync(new GetAccessTokenProcessor { Plaintext = true }, ProfileWith(AuthMethod.Browser)));
        Assert.Equal(result.ExpiresOn.ToUnixTimeSeconds(), result.ExpiresOnUnixTime);
    }

    [Fact]
    public async Task ProcessAsync_PopulatesUserIdFromProfile()
    {
        InitDi(ClientType.CLI, AuthReturning("secret").Object);
        var result = Assert.IsType<AccessTokenResult>(
            await RunAsync(new GetAccessTokenProcessor { Plaintext = true }, ProfileWith(AuthMethod.Browser)));
        Assert.Equal("user@contoso.com", result.UserId);
    }

    [Fact]
    public async Task ProcessAsync_ResourceType_ResolvesScopePassedToAuthService()
    {
        string[]? capturedScopes = null;
        InitDi(ClientType.CLI, AuthReturning("secret", (s, _) => capturedScopes = s).Object);
        await RunAsync(new GetAccessTokenProcessor { ResourceType = "key-vault", Plaintext = true }, ProfileWith(AuthMethod.Browser));
        Assert.Equal(["https://vault.azure.net/.default"], capturedScopes);
    }
}
