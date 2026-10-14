// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Accounts.Models;
using Microsoft.Azure.Azclips.Extensions.Accounts.Processors;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cli.Tests;

/// <summary>
/// Behavioral tests for <see cref="GetAccessTokenProcessor"/>: tenant-compatibility validation for
/// managed identity accounts, secure-by-default token rendering with per-surface plaintext opt-in
/// (CLI <c>--plaintext</c> flag, PowerShell <c>-AsPlainText</c> switch), and scope resolution flowing through to the
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
        AzclipsServiceProvider.Initialize(
            new ClientMetadata(clientType, "00000000-0000-0000-0000-000000000000"));
    }

    private static Profile ProfileWith(AuthMethod method, string tenantId = "tid", string? managedIdentityId = null) => new()
    {
        Name = "p",
        TenantId = tenantId,
        SubscriptionId = "sub-1",
        CloudName = "AzureCloud",
        AuthMethod = method,
        UserPrincipalName = "user@contoso.com",
        ManagedIdentityId = managedIdentityId,
    };

    private static GetAccessTokenProcessor InitProcessor(GetAccessTokenProcessor processor, Profile? profile, out FakeOutputHandler output, CancellationToken publisherToken = default)
    {
        output = new FakeOutputHandler();
        var context = new ProcessorContext(
            new FakeHttpPipeline((_, _) => throw new NotSupportedException()),
            new FakeEventPublisher { Token = publisherToken },
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
    public async Task ProcessAsync_NullProfile_ThrowsWithCliSignInGuidance()
    {
        InitDi(ClientType.CLI, AuthReturning("secret").Object);
        var processor = InitProcessor(new GetAccessTokenProcessor(), profile: null, out _);
        var ex = await Assert.ThrowsAsync<UsageErrorException>(() => processor.ProcessAsync());
        Assert.Contains("az login", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_NullProfile_PowerShellUsesConnectAzAccountGuidance()
    {
        InitDi(ClientType.PowerShell, AuthReturning("secret").Object);
        var processor = InitProcessor(new GetAccessTokenProcessor(), profile: null, out _);
        var ex = await Assert.ThrowsAsync<UsageErrorException>(() => processor.ProcessAsync());
        Assert.Contains("Connect-AzAccount", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_ManagedIdentity_MismatchedExplicitTenant_Throws()
    {
        InitDi(ClientType.CLI, AuthReturning("secret").Object);
        var processor = InitProcessor(
            new GetAccessTokenProcessor { Tenant = "other-tenant" },
            ProfileWith(AuthMethod.ManagedIdentity, tenantId: "mi-tenant"), out _);

        var ex = await Assert.ThrowsAsync<UsageErrorException>(() => processor.ProcessAsync());
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

        var ex = await Assert.ThrowsAsync<UsageErrorException>(() => processor.ProcessAsync());
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
    public async Task ProcessAsync_PopulatesExpiry()
    {
        InitDi(ClientType.CLI, AuthReturning("secret").Object);
        var result = Assert.IsType<AccessTokenResult>(
            await RunAsync(new GetAccessTokenProcessor { Plaintext = true }, ProfileWith(AuthMethod.Browser)));
        // The expiry flows through from the acquired token (AuthReturning sets it one hour out).
        Assert.True(result.ExpiresOn > DateTimeOffset.UtcNow);
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
    public async Task ProcessAsync_PopulatesSubscriptionFromActiveProfile()
    {
        // get-access-token has no subscription input; the reported subscription always comes from
        // the active profile's context.
        InitDi(ClientType.CLI, AuthReturning("secret").Object);
        var result = Assert.IsType<AccessTokenResult>(
            await RunAsync(new GetAccessTokenProcessor { Plaintext = true }, ProfileWith(AuthMethod.Browser)));
        Assert.Equal("sub-1", result.SubscriptionId);
    }

    [Fact]
    public async Task ProcessAsync_SilentAcquisitionFails_ThrowsActionableSignInError()
    {
        var cause = new Exception("AADSTS50076: interaction required.");
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.AcquireTokenSilentlyAsync(
                It.IsAny<string[]?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<Profile?>()))
            .ThrowsAsync(cause);
        InitDi(ClientType.CLI, auth.Object);

        var processor = InitProcessor(
            new GetAccessTokenProcessor { Tenant = "other-tenant" },
            ProfileWith(AuthMethod.Browser), out _);

        var ex = await Assert.ThrowsAsync<AzclipsErrorException>(() => processor.ProcessAsync());
        Assert.Equal("NotSignedIn", ex.Code);
        Assert.Contains("other-tenant", ex.Message, StringComparison.Ordinal);
        Assert.Contains("az login --tenant-id other-tenant", ex.Message, StringComparison.Ordinal);
        // The underlying cause is preserved in the message and as the inner exception for diagnosis.
        Assert.Contains("AADSTS50076", ex.Message, StringComparison.Ordinal);
        Assert.Same(cause, ex.InnerException);
    }

    [Fact]
    public async Task ProcessAsync_SilentAcquisitionFails_PowerShellUsesConnectAzAccountGuidance()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.AcquireTokenSilentlyAsync(
                It.IsAny<string[]?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<Profile?>()))
            .ThrowsAsync(new Exception("interaction required."));
        InitDi(ClientType.PowerShell, auth.Object);

        var processor = InitProcessor(new GetAccessTokenProcessor(), ProfileWith(AuthMethod.Browser), out _);

        var ex = await Assert.ThrowsAsync<AzclipsErrorException>(() => processor.ProcessAsync());
        Assert.Equal("NotSignedIn", ex.Code);
        Assert.Contains("Connect-AzAccount -TenantId", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_ManagedIdentityAcquisitionFails_UsesIdentitySignInGuidance()
    {
        // A managed identity has no interactive, tenant-scoped sign-in (login rejects --identity with
        // --tenant-id), so an MI acquisition failure must point at the identity path, not az login --tenant-id.
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.AcquireTokenSilentlyAsync(
                It.IsAny<string[]?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<Profile?>()))
            .ThrowsAsync(new Exception("IMDS endpoint unavailable."));
        InitDi(ClientType.CLI, auth.Object);

        var processor = InitProcessor(
            new GetAccessTokenProcessor(),
            ProfileWith(AuthMethod.ManagedIdentity, tenantId: "mi-tenant"), out _);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());
        Assert.Contains("az login --identity", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("--tenant-id", ex.Message, StringComparison.Ordinal);
        // The underlying cause is preserved for diagnosis.
        Assert.Contains("IMDS endpoint unavailable", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_ManagedIdentityAcquisitionFails_PowerShellUsesConnectAzAccountIdentity()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.AcquireTokenSilentlyAsync(
                It.IsAny<string[]?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<Profile?>()))
            .ThrowsAsync(new Exception("IMDS endpoint unavailable."));
        InitDi(ClientType.PowerShell, auth.Object);

        var processor = InitProcessor(
            new GetAccessTokenProcessor(),
            ProfileWith(AuthMethod.ManagedIdentity, tenantId: "mi-tenant"), out _);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());
        Assert.Contains("Connect-AzAccount -Identity", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("-TenantId", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_UserAssignedManagedIdentityFails_CliGuidanceCarriesClientIdSelector()
    {
        // A user-assigned managed identity is selected by a stored client id. The recovery guidance must
        // carry that selector so re-establishing the context targets the same identity, not system-assigned.
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.AcquireTokenSilentlyAsync(
                It.IsAny<string[]?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<Profile?>()))
            .ThrowsAsync(new Exception("IMDS endpoint unavailable."));
        InitDi(ClientType.CLI, auth.Object);

        var processor = InitProcessor(
            new GetAccessTokenProcessor(),
            ProfileWith(AuthMethod.ManagedIdentity, tenantId: "mi-tenant",
                managedIdentityId: "MSIClient@11111111-1111-1111-1111-111111111111"), out _);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());
        Assert.Contains("az login --identity --client-id 11111111-1111-1111-1111-111111111111", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_UserAssignedManagedIdentityFails_PowerShellGuidanceCarriesResourceIdSelector()
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.AcquireTokenSilentlyAsync(
                It.IsAny<string[]?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<Profile?>()))
            .ThrowsAsync(new Exception("IMDS endpoint unavailable."));
        InitDi(ClientType.PowerShell, auth.Object);

        var processor = InitProcessor(
            new GetAccessTokenProcessor(),
            ProfileWith(AuthMethod.ManagedIdentity, tenantId: "mi-tenant",
                managedIdentityId: "MSIResource@/subscriptions/s/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/mi"), out _);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());
        Assert.Contains("Connect-AzAccount -Identity -ResourceId /subscriptions/s/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/mi", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_ForwardsSessionCancellationTokenToAuthService()
    {
        // The session/stop token (published via the event publisher) must reach the MSAL acquisition so
        // Ctrl+C / StopProcessing() cancels promptly instead of blocking on the auth/network call.
        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.AcquireTokenSilentlyAsync(
                It.IsAny<string[]?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<Profile?>()))
            .Callback<string[]?, string?, bool, CancellationToken, Profile?>((_, _, _, ct, _) => captured = ct)
            .ReturnsAsync(new AuthResult
            {
                AccessToken = "secret",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                Scopes = Array.Empty<string>(),
                TenantId = "tid",
            });
        InitDi(ClientType.CLI, auth.Object);

        var processor = InitProcessor(
            new GetAccessTokenProcessor(), ProfileWith(AuthMethod.Browser), out _, cts.Token);
        await processor.ProcessAsync();

        Assert.Equal(cts.Token, captured);
    }

    [Fact]
    public async Task ProcessAsync_AcquisitionRaisesUsageError_PropagatesWithoutSignInWrapper()
    {
        // A managed-identity multi-scope request raises AzclipsErrorException.UsageError while the
        // credential resolves the scope, i.e. from inside the acquisition call. The processor's
        // sign-in-failure catch must not swallow that structured usage error and rewrap it as a
        // misleading "could not acquire / run az login" message.
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.AcquireTokenSilentlyAsync(
                It.IsAny<string[]?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<Profile?>()))
            .ThrowsAsync(AzclipsErrorException.UsageError(
                "Managed identity supports a single resource per token request; multiple scopes cannot be requested for a managed identity account."));
        InitDi(ClientType.CLI, auth.Object);

        var processor = InitProcessor(
            new GetAccessTokenProcessor { Scope = ["https://a/.default", "https://b/.default"] },
            ProfileWith(AuthMethod.ManagedIdentity, tenantId: "mi-tenant"), out _);

        var ex = await Assert.ThrowsAsync<UsageErrorException>(() => processor.ProcessAsync());
        Assert.Contains("single resource", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Could not acquire", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_ResourceType_ResolvesScopePassedToAuthService()
    {
        string[]? capturedScopes = null;
        InitDi(ClientType.CLI, AuthReturning("secret", (s, _) => capturedScopes = s).Object);
        await RunAsync(new GetAccessTokenProcessor { ResourceType = "key-vault", Plaintext = true }, ProfileWith(AuthMethod.Browser));
        Assert.Equal(["https://vault.azure.net/.default"], capturedScopes!);
    }
}
