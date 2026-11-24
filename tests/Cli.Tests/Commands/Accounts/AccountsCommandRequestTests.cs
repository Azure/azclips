// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Engine;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static Cli.Tests.Commands.CommandTestHelpers;
using AccountsIndex = Microsoft.Azure.Azclips.Extensions.Accounts.ExtensionIndex;

namespace Cli.Tests.Commands.Accounts;

/// <summary>
/// Runs Accounts CLI commands through the real engine and checks the calls each one makes to the profile, auth and
/// subscription services, which stand in for the request. The service mocks are strict; the constructor answers the
/// active profile read that several commands make, and each test sets up the other calls its command makes.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class AccountsCommandRequestTests : IDisposable
{
    private const string ExtensionName = "Accounts";
    private const string SessionProfileName = "test";
    private const string ProfileTenantId = "33333333-3333-3333-3333-333333333333";
    private const string OtherTenantId = "22222222-2222-2222-2222-222222222222";
    private const string LoginSubscriptionId = "aaaaaaaa-0000-0000-0000-000000000001";
    private const string ClientId = "bbbbbbbb-0000-0000-0000-000000000002";
    private const string ClientSecret = "app-secret";
    private const string WorkProfileName = "work";
    private const string LoginToken = "login-token";
    private const string AccessToken = "access-token";

    private static readonly DateTimeOffset TokenExpiry = DateTimeOffset.UnixEpoch.AddYears(60);

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly Mock<IProfileService> _mockProfiles = new(MockBehavior.Strict);
    private readonly Mock<IAuthService> _mockAuth = new(MockBehavior.Strict);
    private readonly Mock<ISubscriptionService> _mockSubscriptions = new(MockBehavior.Strict);
    private readonly Mock<ILoggingService> _mockLogging = new(MockBehavior.Strict);
    private readonly List<object> _output = [];
    private readonly Profile _workProfile =
        new() { Name = WorkProfileName, TenantId = ProfileTenantId, SubscriptionId = LoginSubscriptionId, CloudName = "AzureCloud" };
    private readonly AuthResult _authResult = new() { AccessToken = LoginToken, ExpiresOn = TokenExpiry, TenantId = TenantId, Scopes = [] };
    private readonly AuthResult _tokenResult = new() { AccessToken = AccessToken, ExpiresOn = TokenExpiry, TenantId = TenantId, Scopes = [] };
    private readonly Engine _engine;

    public AccountsCommandRequestTests()
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

        _mockProfiles.Setup(p => p.GetActiveProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_workProfile);
    }

    public void Dispose()
    {
        _engine.Dispose();
        AzclipsServiceProvider.Dispose();
    }

    // ── profile set ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ProfileSet_ActivatesNamedProfile()
    {
        _mockProfiles.Setup(p => p.GetProfileAsync(WorkProfileName, It.IsAny<CancellationToken>())).ReturnsAsync(_workProfile);
        _mockProfiles.Setup(p => p.SetActiveProfileNameAsync(WorkProfileName, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["profile", "set", "--name", WorkProfileName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        _mockProfiles.Verify(p => p.SetActiveProfileNameAsync(WorkProfileName, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── profile remove ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ProfileRemove_RemovesNamedProfile()
    {
        _mockProfiles.Setup(p => p.RemoveProfileAsync(WorkProfileName, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["profile", "remove", "--name", WorkProfileName]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        _mockProfiles.Verify(p => p.RemoveProfileAsync(WorkProfileName, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── profile show ───────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData]
    [InlineData("--active")]
    public async Task RunAsync_ProfileShowWithoutName_ReadsActiveProfile(params string[] args)
    {
        _mockProfiles.Setup(p => p.GetActiveProfileNameAsync(It.IsAny<CancellationToken>())).ReturnsAsync(WorkProfileName);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["profile", "show", .. args]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        _mockProfiles.Verify(p => p.GetActiveProfileAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── logout ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_LogoutWithActiveProfile_LogsOutActiveProfile()
    {
        SetUpLogout();
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["logout"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        _mockAuth.Verify(a => a.LogoutAsync(WorkProfileName, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_LogoutWithActiveProfile_ClearsActiveProfile()
    {
        SetUpLogout();
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["logout"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        _mockProfiles.Verify(p => p.SetActiveProfileNameAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_LogoutWithoutActiveProfile_DoesNotLogOut()
    {
        _mockProfiles.Setup(p => p.GetActiveProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Profile?)null);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["logout"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        _mockAuth.Verify(a => a.LogoutAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── account get-access-token ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_GetAccessTokenWithoutResource_RequestsArmScope()
    {
        List<TokenRequest> tokenRequests = CaptureTokenRequests();
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["account", "get-access-token"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(["https://management.core.windows.net//.default"], Assert.Single(tokenRequests).Scopes);
    }

    [Fact]
    public async Task RunAsync_GetAccessTokenResource_RequestsResourceDefaultScope()
    {
        List<TokenRequest> tokenRequests = CaptureTokenRequests();
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["account", "get-access-token", "--resource", "https://vault.azure.net"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(["https://vault.azure.net/.default"], Assert.Single(tokenRequests).Scopes);
    }

    [Fact]
    public async Task RunAsync_GetAccessTokenResourceType_RequestsWellKnownResourceScope()
    {
        List<TokenRequest> tokenRequests = CaptureTokenRequests();
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["account", "get-access-token", "--resource-type", "ms-graph"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(["https://graph.microsoft.com//.default"], Assert.Single(tokenRequests).Scopes);
    }

    [Fact]
    public async Task RunAsync_GetAccessTokenScopes_RequestsScopesAsGiven()
    {
        List<TokenRequest> tokenRequests = CaptureTokenRequests();
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["account", "get-access-token", "--scope", "api://app/read", "api://app/write"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(["api://app/read", "api://app/write"], Assert.Single(tokenRequests).Scopes);
    }

    [Fact]
    public async Task RunAsync_GetAccessTokenWithoutTenant_RequestsProfileTenant()
    {
        List<TokenRequest> tokenRequests = CaptureTokenRequests();
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["account", "get-access-token"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(TenantId, Assert.Single(tokenRequests).TenantId);
    }

    [Theory]
    [InlineData("--tenant-id")]
    [InlineData("--tenant")]
    [InlineData("-t")]
    public async Task RunAsync_GetAccessTokenTenantFlag_RequestsGivenTenant(string tenantFlag)
    {
        List<TokenRequest> tokenRequests = CaptureTokenRequests();
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["account", "get-access-token", tenantFlag, OtherTenantId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(OtherTenantId, Assert.Single(tokenRequests).TenantId);
    }

    [Fact]
    public async Task RunAsync_GetAccessToken_RequestsTokenWithoutPrompt()
    {
        List<TokenRequest> tokenRequests = CaptureTokenRequests();
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["account", "get-access-token"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.False(Assert.Single(tokenRequests).PromptOnError);
    }

    [Fact]
    public async Task RunAsync_GetAccessToken_RequestsTokenForSessionProfile()
    {
        List<TokenRequest> tokenRequests = CaptureTokenRequests();
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["account", "get-access-token"]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(SessionProfileName, Assert.Single(tokenRequests).Profile?.Name);
    }

    // ── login ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_LoginClientSecret_SignsInWithClientSecretCredential()
    {
        List<LoginRequest> loginRequests = CaptureLoginRequests();
        CaptureSavedProfiles();
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["login", "--client-id", ClientId, "--client-secret", ClientSecret, "--tenant-id", TenantId, "--subscription-id", LoginSubscriptionId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(new AuthCredential.ClientSecret(ClientId, ClientSecret), Assert.Single(loginRequests).Credential);
    }

    [Theory]
    [InlineData("--tenant-id")]
    [InlineData("--tenant")]
    [InlineData("-t")]
    public async Task RunAsync_LoginClientSecretTenantFlag_SignsInToGivenTenant(string tenantFlag)
    {
        List<LoginRequest> loginRequests = CaptureLoginRequests();
        CaptureSavedProfiles();
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["login", "--client-id", ClientId, "--client-secret", ClientSecret, tenantFlag, TenantId, "--subscription-id", LoginSubscriptionId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(TenantId, Assert.Single(loginRequests).TenantId);
    }

    [Theory]
    [InlineData("--subscription-id")]
    [InlineData("--subscription")]
    [InlineData("-s")]
    public async Task RunAsync_LoginClientSecretSubscriptionFlag_ChecksSubscriptionWithSignInToken(string subscriptionFlag)
    {
        CaptureLoginRequests();
        CaptureSavedProfiles();
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["login", "--client-id", ClientId, "--client-secret", ClientSecret, "--tenant-id", TenantId, subscriptionFlag, LoginSubscriptionId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        _mockSubscriptions.Verify(s => s.GetSubscriptionByIdAsync(LoginToken, LoginSubscriptionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_LoginWithoutOutProfile_SavesDefaultProfile()
    {
        CaptureLoginRequests();
        List<Profile> savedProfiles = CaptureSavedProfiles();
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["login", "--client-id", ClientId, "--client-secret", ClientSecret, "--tenant-id", TenantId, "--subscription-id", LoginSubscriptionId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("default", Assert.Single(savedProfiles).Name);
    }

    [Fact]
    public async Task RunAsync_LoginOutProfile_SavesNamedProfile()
    {
        CaptureLoginRequests();
        List<Profile> savedProfiles = CaptureSavedProfiles();
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "login",
                "--client-id", ClientId,
                "--client-secret", ClientSecret,
                "--tenant-id", TenantId,
                "--subscription-id", LoginSubscriptionId,
                "--out-profile", "ci",
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal("ci", Assert.Single(savedProfiles).Name);
    }

    [Fact]
    public async Task RunAsync_LoginClientSecret_SavesProfileWithClientId()
    {
        CaptureLoginRequests();
        List<Profile> savedProfiles = CaptureSavedProfiles();
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            ["login", "--client-id", ClientId, "--client-secret", ClientSecret, "--tenant-id", TenantId, "--subscription-id", LoginSubscriptionId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(ClientId, Assert.Single(savedProfiles).ClientId);
    }

    [Fact]
    public async Task RunAsync_LoginIdentity_SignsInWithSystemAssignedIdentity()
    {
        List<LoginRequest> loginRequests = CaptureLoginRequests();
        CaptureSavedProfiles();
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["login", "--identity", "--subscription-id", LoginSubscriptionId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(new AuthCredential.ManagedIdentity(ManagedIdentityIdType.SystemAssigned), Assert.Single(loginRequests).Credential);
    }

    [Fact]
    public async Task RunAsync_LoginIdentityClientId_SignsInWithUserAssignedIdentity()
    {
        List<LoginRequest> loginRequests = CaptureLoginRequests();
        CaptureSavedProfiles();
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["login", "-i", "--client-id", ClientId, "--subscription-id", LoginSubscriptionId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(new AuthCredential.ManagedIdentity(ManagedIdentityIdType.ClientId, ClientId), Assert.Single(loginRequests).Credential);
    }

    [Fact]
    public async Task RunAsync_LoginFederatedToken_SignsInWithFederatedCredential()
    {
        List<LoginRequest> loginRequests = CaptureLoginRequests();
        CaptureSavedProfiles();
        CommandInvocation invocation = CreateInvocation(
            ExtensionName,
            [
                "login",
                "--service-principal",
                "--federated-token", "oidc-token",
                "--client-id", ClientId,
                "--tenant-id", TenantId,
                "--subscription-id", LoginSubscriptionId,
            ]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(new AuthCredential.FederatedIdentity(ClientId, "oidc-token"), Assert.Single(loginRequests).Credential);
    }

    [Fact]
    public async Task RunAsync_LoginSubscriptionOnly_SignsInWithBrowserCredentialWithoutTenant()
    {
        List<LoginRequest> loginRequests = CaptureLoginRequests();
        CaptureSavedProfiles();
        _mockSubscriptions.Setup(s => s.ResolveSubscriptionTenantAsync(LoginToken, LoginSubscriptionId, It.IsAny<CancellationToken>())).ReturnsAsync(TenantId);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["login", "--subscription-id", LoginSubscriptionId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        Assert.Equal(new LoginRequest(null, null, new AuthCredential.Browser()), Assert.Single(loginRequests));
    }

    [Fact]
    public async Task RunAsync_LoginSubscriptionOnly_ResolvesSubscriptionTenant()
    {
        CaptureLoginRequests();
        CaptureSavedProfiles();
        _mockSubscriptions.Setup(s => s.ResolveSubscriptionTenantAsync(LoginToken, LoginSubscriptionId, It.IsAny<CancellationToken>())).ReturnsAsync(TenantId);
        CommandInvocation invocation = CreateInvocation(ExtensionName, ["login", "--subscription-id", LoginSubscriptionId]);
        OutputStreams streams = CreateOutput(_output);
        using ISession session = _engine.OpenExecutionSession(invocation, streams);

        await session.RunAsync();

        _mockSubscriptions.Verify(s => s.ResolveSubscriptionTenantAsync(LoginToken, LoginSubscriptionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // Sets up a token for any silent request and returns the arguments of each request made.
    private List<TokenRequest> CaptureTokenRequests()
    {
        List<TokenRequest> tokenRequests = [];
        _mockAuth
            .Setup(a => a.AcquireTokenSilentlyAsync(It.IsAny<string[]?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<Profile?>()))
            .Callback((string[]? scopes, string? tenantId, bool promptOnError, CancellationToken _, Profile? profile) =>
                tokenRequests.Add(new(scopes, tenantId, promptOnError, profile)))
            .ReturnsAsync(_tokenResult);
        return tokenRequests;
    }

    // Sets up a successful sign-in and subscription check, and returns each login request made.
    private List<LoginRequest> CaptureLoginRequests()
    {
        List<LoginRequest> loginRequests = [];
        _mockAuth.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .Callback((LoginRequest request, CancellationToken _) => loginRequests.Add(request))
            .ReturnsAsync(_authResult);
        _mockSubscriptions.Setup(s => s.GetSubscriptionByIdAsync(LoginToken, LoginSubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionInfo(LoginSubscriptionId, "Development", "Enabled"));
        return loginRequests;
    }

    // Sets up the profile save and returns each profile the login saves.
    private List<Profile> CaptureSavedProfiles()
    {
        List<Profile> savedProfiles = [];
        _mockProfiles.Setup(p => p.SaveAndActivateProfileAsync(It.IsAny<Profile>(), It.IsAny<CancellationToken>()))
            .Callback((Profile profile, CancellationToken _) => savedProfiles.Add(profile))
            .Returns(Task.CompletedTask);
        return savedProfiles;
    }

    private void SetUpLogout()
    {
        _mockAuth.Setup(a => a.LogoutAsync(WorkProfileName, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockProfiles.Setup(p => p.SetActiveProfileNameAsync(null, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    private sealed record TokenRequest(IEnumerable<string>? Scopes, string? TenantId, bool PromptOnError, Profile? Profile);
}
