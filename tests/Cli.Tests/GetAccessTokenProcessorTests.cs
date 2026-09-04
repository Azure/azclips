// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Login.Processors;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cli.Tests;

/// <summary>
/// Behavioral tests for <see cref="GetAccessTokenProcessor"/>: tenant-compatibility validation for
/// managed identity / Cloud Shell accounts, secure-by-default token rendering with per-surface
/// plaintext opt-in (CLI <c>--plaintext</c> flag; PowerShell env var), and scope resolution flowing
/// through to the auth service.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class GetAccessTokenProcessorTests
{
    private const string PlaintextEnvVar = "AZUREPS_OUTPUT_PLAINTEXT_AZACCESSTOKEN";

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

    private static async Task<AccessTokenResult> RunAsync(GetAccessTokenProcessor processor, Profile profile)
    {
        InitProcessor(processor, profile, out var output);
        await processor.ProcessAsync();
        return Assert.IsType<AccessTokenResult>(Assert.Single(output.WrittenValues));
    }

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
        Assert.Equal("secret", result.Token);
    }

    [Fact]
    public async Task ProcessAsync_BrowserAccount_ExplicitDifferentTenant_IsAllowed()
    {
        InitDi(ClientType.CLI, AuthReturning("secret").Object);
        var result = await RunAsync(
            new GetAccessTokenProcessor { Tenant = "other-tenant", Plaintext = true },
            ProfileWith(AuthMethod.Browser, tenantId: "home-tenant"));
        Assert.Equal("secret", result.Token);
    }

    [Fact]
    public async Task ProcessAsync_CliDefault_RedactsToken()
    {
        InitDi(ClientType.CLI, AuthReturning("secret").Object);
        var result = await RunAsync(new GetAccessTokenProcessor(), ProfileWith(AuthMethod.Browser));
        Assert.Equal(AccessTokenResult.RedactedPlaceholder, result.Token);
    }

    [Fact]
    public async Task ProcessAsync_CliPlaintextFlag_RevealsToken()
    {
        InitDi(ClientType.CLI, AuthReturning("secret").Object);
        var result = await RunAsync(new GetAccessTokenProcessor { Plaintext = true }, ProfileWith(AuthMethod.Browser));
        Assert.Equal("secret", result.Token);
    }

    [Fact]
    public async Task ProcessAsync_PowerShellEnvVarSet_RevealsToken()
    {
        var previous = Environment.GetEnvironmentVariable(PlaintextEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(PlaintextEnvVar, "true");
            InitDi(ClientType.PowerShell, AuthReturning("secret").Object);
            var result = await RunAsync(new GetAccessTokenProcessor(), ProfileWith(AuthMethod.Browser));
            Assert.Equal("secret", result.Token);
        }
        finally
        {
            Environment.SetEnvironmentVariable(PlaintextEnvVar, previous);
        }
    }

    [Fact]
    public async Task ProcessAsync_PowerShellNoEnvVar_RedactsToken()
    {
        var previous = Environment.GetEnvironmentVariable(PlaintextEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(PlaintextEnvVar, null);
            InitDi(ClientType.PowerShell, AuthReturning("secret").Object);
            var result = await RunAsync(new GetAccessTokenProcessor(), ProfileWith(AuthMethod.Browser));
            Assert.Equal(AccessTokenResult.RedactedPlaceholder, result.Token);
        }
        finally
        {
            Environment.SetEnvironmentVariable(PlaintextEnvVar, previous);
        }
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
