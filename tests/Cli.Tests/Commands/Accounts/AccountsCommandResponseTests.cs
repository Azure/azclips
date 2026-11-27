// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Engine;
using Microsoft.Azure.Azclips.Extensions.Accounts.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using AccountsIndex = Microsoft.Azure.Azclips.Extensions.Accounts.ExtensionIndex;

namespace Cli.Tests.Commands.Accounts;

/// <summary>
/// Runs Accounts CLI commands through the real engine against canned profile, auth and subscription service results,
/// and checks what each command writes, or the error it throws for a result.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class AccountsCommandResponseTests : IDisposable
{
    private const string ExtensionName = "Accounts";
    private const string OtherTenantId = "22222222-2222-2222-2222-222222222222";
    private const string LoginSubscriptionId = "aaaaaaaa-0000-0000-0000-000000000001";
    private const string ClientId = "bbbbbbbb-0000-0000-0000-000000000002";
    private const string ClientSecret = "app-secret";
    private const string WorkProfileName = "work";
    private const string HomeProfileName = "home";
    private const string MissingProfileName = "missing";
    private const string LoginToken = "login-token";
    private const string AzureCloud = "AzureCloud";
    private const string RawToken = "eyJ.secret.token";

    private static readonly DateTimeOffset TokenExpiry = DateTimeOffset.UnixEpoch.AddYears(60);
    private static readonly DateTimeOffset IssuedTokenExpiry = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly Mock<IProfileService> _mockProfiles = new(MockBehavior.Strict);
    private readonly Mock<IAuthService> _mockAuth = new(MockBehavior.Strict);
    private readonly Mock<ISubscriptionService> _mockSubscriptions = new(MockBehavior.Strict);
    private readonly Mock<ILoggingService> _mockLogging = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Profile _workProfile = new() { Name = WorkProfileName, TenantId = TenantId, SubscriptionId = LoginSubscriptionId, CloudName = AzureCloud };
    private readonly Profile _homeProfile = new() { Name = HomeProfileName, TenantId = TenantId, SubscriptionId = LoginSubscriptionId, CloudName = AzureCloud };
    private readonly AuthResult _authResult = new() { AccessToken = LoginToken, ExpiresOn = TokenExpiry, TenantId = TenantId, Scopes = [] };
    private readonly AuthResult _tokenResult = new() { AccessToken = RawToken, ExpiresOn = TokenExpiry, TenantId = TenantId, Scopes = [] };
    private readonly Engine _engine;

    public AccountsCommandResponseTests()
    {
        // The Accounts processors resolve these services from AzclipsServiceProvider, not from the engine.
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton(_mockProfiles.Object);
            services.AddSingleton(_mockAuth.Object);
            services.AddSingleton(_mockSubscriptions.Object);
            services.AddSingleton(_mockLogging.Object);
        });
        AzclipsServiceProvider.Initialize(new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));
        _engine = CreateEngine(typeof(AccountsIndex).Assembly, _mockWire);

        _mockProfiles.Setup(p => p.GetAllProfilesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([_workProfile, _homeProfile]);
        _mockProfiles.Setup(p => p.GetProfileAsync(WorkProfileName, It.IsAny<CancellationToken>())).ReturnsAsync(_workProfile);
        _mockProfiles.Setup(p => p.GetProfileAsync(HomeProfileName, It.IsAny<CancellationToken>())).ReturnsAsync(_homeProfile);
        _mockProfiles.Setup(p => p.GetProfileAsync(MissingProfileName, It.IsAny<CancellationToken>())).ReturnsAsync((Profile?)null);
        _mockProfiles.Setup(p => p.GetActiveProfileNameAsync(It.IsAny<CancellationToken>())).ReturnsAsync(WorkProfileName);
        _mockProfiles.Setup(p => p.GetActiveProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_workProfile);
        _mockProfiles.Setup(p => p.SetActiveProfileNameAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockProfiles.Setup(p => p.RemoveProfileAsync(WorkProfileName, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockProfiles.Setup(p => p.RemoveProfileAsync(MissingProfileName, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _mockProfiles.Setup(p => p.SaveAndActivateProfileAsync(It.IsAny<Profile>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockAuth.Setup(a => a.LogoutAsync(WorkProfileName, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockAuth.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(_authResult);
        _mockSubscriptions.Setup(s => s.GetSubscriptionByIdAsync(LoginToken, LoginSubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionInfo(LoginSubscriptionId, "Development", "Enabled"));
        SetupToken(_tokenResult);
    }

    public void Dispose()
    {
        _engine.Dispose();
        AzclipsServiceProvider.Dispose();
    }

    // ── profile list ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ProfileList_WritesEveryProfileName()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["profile", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([WorkProfileName, HomeProfileName], Assert.IsType<List<ProfileResult>>(Assert.Single(_output)).Select(p => p.Name));
    }

    [Fact]
    public async Task RunAsync_ProfileList_MarksOnlyActiveProfileIgnoringCase()
    {
        _mockProfiles.Setup(p => p.GetActiveProfileNameAsync(It.IsAny<CancellationToken>())).ReturnsAsync("HOME");
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["profile", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal([false, true], Assert.IsType<List<ProfileResult>>(Assert.Single(_output)).Select(p => p.IsActive));
    }

    [Fact]
    public async Task RunAsync_ProfileListWithoutProfiles_WritesEmptyList()
    {
        _mockProfiles.Setup(p => p.GetAllProfilesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["profile", "list"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(Assert.IsType<List<ProfileResult>>(Assert.Single(_output)));
    }

    // ── profile set ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ProfileSet_WritesProfileAsActive()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["profile", "set", "--name", WorkProfileName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            new ProfileResult
            {
                Name = WorkProfileName,
                TenantId = TenantId,
                SubscriptionId = LoginSubscriptionId,
                CloudName = AzureCloud,
                AuthMethod = AuthMethod.Browser,
                IsActive = true,
            },
            Assert.Single(_output));
    }

    [Fact]
    public async Task RunAsync_ProfileSetUnknownName_ThrowsProfileNotFound()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["profile", "set", "--name", MissingProfileName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(("ProfileNotFound", "Profile 'missing' does not exist."), CodeAndMessage(exception));
    }

    // ── profile remove ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ProfileRemove_WritesNothing()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["profile", "remove", "--name", WorkProfileName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(_output);
    }

    [Fact]
    public async Task RunAsync_ProfileRemoveUnknownName_ThrowsProfileNotFound()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["profile", "remove", "--name", MissingProfileName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(("ProfileNotFound", "Profile 'missing' does not exist."), CodeAndMessage(exception));
    }

    // ── profile show ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ProfileShowInactiveName_WritesProfileAsInactive()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["profile", "show", "--name", HomeProfileName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            new ProfileResult
            {
                Name = HomeProfileName,
                TenantId = TenantId,
                SubscriptionId = LoginSubscriptionId,
                CloudName = AzureCloud,
                AuthMethod = AuthMethod.Browser,
                IsActive = false,
            },
            Assert.Single(_output));
    }

    [Fact]
    public async Task RunAsync_ProfileShowActive_WritesProfileAsActive()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["profile", "show", "--active"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            new ProfileResult
            {
                Name = WorkProfileName,
                TenantId = TenantId,
                SubscriptionId = LoginSubscriptionId,
                CloudName = AzureCloud,
                AuthMethod = AuthMethod.Browser,
                IsActive = true,
            },
            Assert.Single(_output));
    }

    [Fact]
    public async Task RunAsync_ProfileShowWithoutActiveProfile_ThrowsNotSignedIn()
    {
        _mockProfiles.Setup(p => p.GetActiveProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Profile?)null);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["profile", "show"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(("NotSignedIn", "No active profile is set."), CodeAndMessage(exception));
    }

    [Fact]
    public async Task RunAsync_ProfileShowUnknownName_ThrowsProfileNotFound()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["profile", "show", "--name", MissingProfileName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(("ProfileNotFound", "Profile 'missing' does not exist."), CodeAndMessage(exception));
    }

    // ── logout ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_Logout_WritesNothing()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["logout"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Empty(_output);
    }

    // ── account get-access-token ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GetAccessToken_WritesRedactedToken()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["account", "get-access-token"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(AccessTokenResult.RedactedPlaceholder, Assert.IsType<AccessTokenResult>(Assert.Single(_output)).Token);
    }

    [Fact]
    public async Task RunAsync_GetAccessTokenPlaintext_WritesRawToken()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["account", "get-access-token", "--plaintext"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(RawToken, Assert.IsType<AccessTokenResult>(Assert.Single(_output)).Token);
    }

    [Fact]
    public async Task RunAsync_GetAccessToken_WritesTokenExpiry()
    {
        SetupToken(_tokenResult with { ExpiresOn = IssuedTokenExpiry });
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["account", "get-access-token"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(IssuedTokenExpiry, Assert.IsType<AccessTokenResult>(Assert.Single(_output)).ExpiresOn);
    }

    [Fact]
    public async Task RunAsync_GetAccessTokenIssuedForOtherTenant_WritesTenantOfIssuedToken()
    {
        SetupToken(_tokenResult with { TenantId = OtherTenantId });
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["account", "get-access-token"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(OtherTenantId, Assert.IsType<AccessTokenResult>(Assert.Single(_output)).TenantId);
    }

    [Fact]
    public async Task RunAsync_GetAccessToken_WritesSessionProfileSubscription()
    {
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["account", "get-access-token"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(SubscriptionId, Assert.IsType<AccessTokenResult>(Assert.Single(_output)).SubscriptionId);
    }

    [Fact]
    public async Task RunAsync_GetAccessTokenProfileWithoutUser_WritesUserFromToken()
    {
        SetupToken(_tokenResult with { UserPrincipalName = "ada@contoso.com" });
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["account", "get-access-token"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("ada@contoso.com", Assert.IsType<AccessTokenResult>(Assert.Single(_output)).UserId);
    }

    [Fact]
    public async Task RunAsync_GetAccessTokenAcquisitionFails_ThrowsNotSignedInWithSignInCommand()
    {
        _mockAuth
            .Setup(a => a.AcquireTokenSilentlyAsync(It.IsAny<string[]?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<Profile?>()))
            .ThrowsAsync(new InvalidOperationException("No cached account."));
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["account", "get-access-token"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            (
                "NotSignedIn",
                $"Could not acquire an access token for tenant '{TenantId}'. No cached account. You may not be signed in to that tenant, or it requires additional sign-in "
                + "(for example, multi-factor authentication or a conditional access policy). "
                + $"Run 'az login --tenant-id {TenantId} --subscription-id {SubscriptionId}' to authenticate, then retry."),
            CodeAndMessage(exception));
    }

    // ── login ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_LoginClientSecret_WritesSavedProfileAsActive()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["login", "--client-id", ClientId, "--client-secret", ClientSecret, "--tenant-id", TenantId, "--subscription-id", LoginSubscriptionId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(
            new ProfileResult
            {
                Name = "default",
                TenantId = TenantId,
                SubscriptionId = LoginSubscriptionId,
                CloudName = AzureCloud,
                AuthMethod = AuthMethod.ServicePrincipalSecret,
                IsActive = true,
                ClientId = ClientId,
            },
            Assert.Single(_output));
    }

    [Fact]
    public async Task RunAsync_LoginIdentityClientId_WritesManagedIdentityId()
    {
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["login", "--identity", "--client-id", ClientId, "--subscription-id", LoginSubscriptionId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal($"MSIClient@{ClientId}", Assert.IsType<ProfileResult>(Assert.Single(_output)).ManagedIdentityId);
    }

    [Fact]
    public async Task RunAsync_LoginSubscriptionInOtherTenant_WritesSubscriptionOwningTenant()
    {
        _mockSubscriptions.Setup(s => s.ResolveSubscriptionTenantAsync(LoginToken, LoginSubscriptionId, It.IsAny<CancellationToken>())).ReturnsAsync(OtherTenantId);
        _mockLogging.Setup(l => l.Verbose(It.IsAny<string>()));
        SetupToken(_tokenResult with { TenantId = OtherTenantId });
        _mockSubscriptions.Setup(s => s.GetSubscriptionByIdAsync(RawToken, LoginSubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionInfo(LoginSubscriptionId, "Development", "Enabled"));
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["login", "--subscription-id", LoginSubscriptionId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(OtherTenantId, Assert.IsType<ProfileResult>(Assert.Single(_output)).TenantId);
    }

    [Fact]
    public async Task RunAsync_LoginInaccessibleSubscription_ThrowsSubscriptionAccessMessage()
    {
        _mockSubscriptions.Setup(s => s.GetSubscriptionByIdAsync(LoginToken, LoginSubscriptionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("403 Forbidden"));
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["login", "--client-id", ClientId, "--client-secret", ClientSecret, "--tenant-id", TenantId, "--subscription-id", LoginSubscriptionId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        Exception? exception = await Record.ExceptionAsync(session.RunAsync);

        Assert.Equal(
            $"Failed to access subscription '{LoginSubscriptionId}': 403 Forbidden. Make sure the subscription ID is correct and you have the necessary permissions.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    private void SetupToken(AuthResult authResult) =>
        _mockAuth
            .Setup(a => a.AcquireTokenSilentlyAsync(It.IsAny<string[]?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<Profile?>()))
            .ReturnsAsync(authResult);

    private static (string? Code, string? Message) CodeAndMessage(Exception? exception) =>
        exception is AzclipsErrorException error ? (error.Code, error.Message) : (null, exception?.Message);
}
