using Core.Abstractions;
using Core.Agent.AgentMode;
using Core.Agent.Providers;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Contract tests for <see cref="AgentStartupBoundary"/>, the seam that acquires the Azure token
/// and resolves the caller tenant's data boundary before the agent is created.
/// </summary>
public class AgentStartupBoundaryTests
{
    [Theory]
    [InlineData("EU", "Global", BuiltinEndpointResolver.PpeWestEurope)]
    [InlineData("Global", "EU", BuiltinEndpointResolver.PpeEastUs)]
    public async Task RecoveryProfile_DrivesBothTokenAndModelBoundary(
        string commandBoundary, string activeBoundary, string expectedEndpoint)
    {
        var commandProfile = new Profile
        {
            Name = "command-profile", TenantId = "command-tenant",
            AuthMethod = AuthMethod.Browser, UserPrincipalName = "command@example.test",
        };
        var profiles = new DifferentProfileService(commandProfile);
        var auth = new CountingAuthService();
        var context = new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), true,
            UploadRegion: UploadRegion.ROW);
        using var root = new System.Diagnostics.Activity("recovery").Start();
        Core.Telemetry.RecoveryTelemetry.TagRecoveryContext(root, context);

        var startup = await AgentStartupBoundary.ResolveAsync(
            Options(), auth, profiles, handedOverBoundary: null,
            (tenant, token, _) =>
            {
                Assert.Equal(CountingAuthService.IssuedToken, token);
                return Task.FromResult<string?>(tenant == commandProfile.TenantId ? commandBoundary : activeBoundary);
            }, root, CancellationToken.None, commandProfile.Name);

        Assert.Null(startup.Failure);
        Assert.Same(commandProfile, auth.RequestedProfile);
        Assert.Equal(commandProfile.TenantId, auth.RequestedTenant);
        Assert.Equal(0, profiles.ActiveReads);
        Assert.Equal(1, auth.AcquireCount);
        Assert.Equal(commandBoundary, startup.DataBoundary);
        Assert.Equal(expectedEndpoint, BuiltinEndpointResolver.Choose(startup.DataBoundary).Url);
        Assert.Equal("ROW", root.GetTagItem(TelemetryTags.UploadRegion));
    }

    [Fact]
    public async Task MissingRecoveryProfile_FailsWithoutAcquiringAnActiveProfileToken()
    {
        var auth = new CountingAuthService();
        var profiles = new DifferentProfileService(new Profile { Name = "exists", TenantId = TenantId });
        var lookup = new RecordingLookup("Global");
        var startup = await AgentStartupBoundary.ResolveAsync(
            Options(), auth, profiles, null, lookup.Invoke, null, CancellationToken.None, "missing");

        Assert.NotNull(startup.Failure);
        Assert.Contains("missing", Assert.Throws<InvalidOperationException>(() => startup.Failure.Throw()).Message);
        Assert.Null(startup.Auth);
        Assert.Equal(0, profiles.ActiveReads);
        Assert.Equal(0, auth.AcquireCount);
        Assert.Equal(0, lookup.Calls);
    }

    [Fact]
    public async Task Standalone_UsesOneActiveProfileSnapshotForTokenAndBoundary()
    {
        var profile = new Profile { Name = "active", TenantId = "active-tenant" };
        var profiles = new DifferentProfileService(profile);
        var auth = new CountingAuthService();
        var lookup = new RecordingLookup("Global");
        var startup = await StartAsync(auth, lookup.Invoke, profiles: profiles);

        Assert.Null(startup.Failure);
        Assert.Equal(1, profiles.ActiveReads);
        Assert.Same(profiles.ActiveProfile, auth.RequestedProfile);
        Assert.Equal(profiles.ActiveProfile.TenantId, auth.RequestedTenant);
        Assert.Equal(auth.RequestedTenant, lookup.TenantId);
    }

    private const string TenantId = "fake-tenant";

    private static AgentOptions Options(string? provider = "builtin") => new() { Provider = provider! };

    private static Task<StartupBoundary> StartAsync(
        IAuthService auth,
        DataBoundaryLookup lookup,
        string? handedOver = null,
        IProfileService? profiles = null,
        string? provider = "builtin",
        CancellationToken ct = default) =>
        AgentStartupBoundary.ResolveAsync(
            Options(provider), auth, profiles ?? new StubProfileService(), handedOver, lookup, session: null, ct);

    [Fact]
    public async Task HandedOverBoundary_IsUsedWithoutALookup()
    {
        var lookup = new RecordingLookup("Global");
        var auth = new CountingAuthService();

        var startup = await StartAsync(auth, lookup.Invoke, handedOver: "EU");

        Assert.Equal("EU", startup.DataBoundary);
        Assert.Equal(0, lookup.Calls);
        Assert.Equal(1, auth.AcquireCount);
        Assert.NotNull(startup.Auth);
        Assert.Null(startup.Failure);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unknown")]
    public async Task MissingHandedOverBoundary_LooksUpWithTheAcquiredToken(string? handedOver)
    {
        // One MSAL flow serves both the model provider and the lookup.
        var lookup = new RecordingLookup("EU");
        var auth = new CountingAuthService();

        var startup = await StartAsync(auth, lookup.Invoke, handedOver);

        Assert.Equal("EU", startup.DataBoundary);
        Assert.Equal(1, lookup.Calls);
        Assert.Equal(TenantId, lookup.TenantId);
        Assert.Equal(CountingAuthService.IssuedToken, lookup.Token);
        Assert.Equal(1, auth.AcquireCount);
        Assert.Null(startup.Failure);
    }

    [Fact]
    public async Task TokenFailure_SurfacesTheFailureAndSkipsTheLookup()
    {
        // A signed-out user still gets the actionable "run az login" from the caller.
        var lookup = new RecordingLookup("EU");

        var startup = await StartAsync(new ThrowingAuthService(), lookup.Invoke);

        Assert.Null(startup.Auth);
        Assert.NotNull(startup.Failure);
        Assert.Null(startup.DataBoundary);
        Assert.Equal(0, lookup.Calls);
    }

    [Fact]
    public async Task CallerCancellation_IsSurfacedWithItsOriginalException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var startup = await StartAsync(new CancellingAuthService(), new RecordingLookup("EU").Invoke, ct: cts.Token);

        Assert.NotNull(startup.Failure);
        Assert.ThrowsAny<OperationCanceledException>(() => startup.Failure!.Throw());
    }

    [Fact]
    public async Task CancellationSwallowedByTheLookup_IsStillSurfaced()
    {
        // Defensive coverage for a lookup that returns normally after caller cancellation.
        using var cts = new CancellationTokenSource();
        DataBoundaryLookup swallowing = (_, _, _) =>
        {
            cts.Cancel();
            return Task.FromResult<string?>(null);
        };

        var startup = await StartAsync(new CountingAuthService(), swallowing, ct: cts.Token);

        Assert.Null(startup.DataBoundary);
        Assert.NotNull(startup.Auth);
        Assert.NotNull(startup.Failure);
        Assert.ThrowsAny<OperationCanceledException>(() => startup.Failure!.Throw());
    }

    [Fact]
    public async Task LookupTimeoutWithoutCallerCancellation_DoesNotFailStartup()
    {
        // Mirrors Core's resolver: an internal deadline cancels a slow ARM call and yields null.
        using var caller = new CancellationTokenSource();
        var timedOut = false;
        DataBoundaryLookup slowArm = async (_, _, ct) =>
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromMilliseconds(20));
            try
            {
                await Task.Delay(Timeout.Infinite, deadline.Token);
                return "EU";
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                timedOut = true;
                return null;
            }
        };

        var startup = await StartAsync(new CountingAuthService(), slowArm, ct: caller.Token);

        Assert.True(timedOut, "the lookup must reach its own deadline for this case to mean anything.");
        Assert.False(caller.IsCancellationRequested);
        Assert.Null(startup.DataBoundary);
        Assert.Equal(CountingAuthService.IssuedToken, startup.Auth!.AccessToken);
        Assert.Null(startup.Failure);
    }

    [Theory]
    [InlineData("github")]
    [InlineData("GitHub")]
    [InlineData("openai")]
    [InlineData(null)]
    public async Task NonBuiltinProvider_NeverAcquiresATokenOrLooksUp(string? provider)
    {
        var lookup = new RecordingLookup("EU");
        var auth = new CountingAuthService();

        var startup = await StartAsync(auth, lookup.Invoke, handedOver: "Global", provider: provider);

        Assert.Equal(0, auth.AcquireCount);
        Assert.Equal(0, lookup.Calls);
        Assert.Equal("Global", startup.DataBoundary);
        Assert.Null(startup.Auth);
        Assert.Null(startup.Failure);
    }

    [Fact]
    public async Task SignedOutProfile_KeepsTheTokenAndSkipsTheLookup()
    {
        var lookup = new RecordingLookup("EU");

        var startup = await StartAsync(new CountingAuthService(), lookup.Invoke, profiles: new SignedOutProfileService());

        Assert.Null(startup.DataBoundary);
        Assert.NotNull(startup.Auth);
        Assert.Equal(0, lookup.Calls);
    }

    [Fact]
    public async Task LookupFailure_KeepsTheTokenAndDoesNotBlockRecovery()
    {
        var throwingLookup = await StartAsync(
            new CountingAuthService(), (_, _, _) => throw new InvalidOperationException("ARM is down"));
        var noAnswer = await StartAsync(new CountingAuthService(), new RecordingLookup(null).Invoke);

        foreach (var startup in new[] { throwingLookup, noAnswer })
        {
            Assert.Null(startup.DataBoundary);
            Assert.Equal(CountingAuthService.IssuedToken, startup.Auth!.AccessToken);
            Assert.Null(startup.Failure);
        }

    }

    [Fact]
    public async Task ProfileFailure_IsSurfacedBeforeTokenAcquisition()
    {
        var auth = new CountingAuthService();
        var startup = await StartAsync(auth, new RecordingLookup("EU").Invoke, profiles: new ThrowingProfileService());
        Assert.NotNull(startup.Failure);
        Assert.Null(startup.Auth);
        Assert.Equal(0, auth.AcquireCount);
    }

    private sealed class RecordingLookup(string? answer)
    {
        public int Calls { get; private set; }
        public string? TenantId { get; private set; }
        public string? Token { get; private set; }

        public Task<string?> Invoke(string tenantId, string accessToken, CancellationToken ct)
        {
            Calls++;
            TenantId = tenantId;
            Token = accessToken;
            return Task.FromResult(answer);
        }
    }

    [Fact]
    public async Task BuiltinEndpointFallback_AcquiresTheTokenOnce()
    {
        // No host token and an automatic endpoint: one token covers the lookup and the provider.
        var lookup = new RecordingLookup("Global");
        var auth = new CountingAuthService();

        var (endpoint, acquired) = await AgentStartupBoundary.ResolveBuiltinEndpointAsync(
            Options(), auth, new StubProfileService(), preAcquiredAuth: null, dataBoundary: null,
            lookup.Invoke, CancellationToken.None);

        Assert.Equal(1, auth.AcquireCount);
        Assert.Equal(1, lookup.Calls);
        Assert.Equal(CountingAuthService.IssuedToken, acquired?.AccessToken);
        Assert.Equal(Core.Agent.Providers.BuiltinEndpointResolver.PpeEastUs, endpoint.Url);
    }

    [Fact]
    public async Task BuiltinEndpointFallback_CancelledLookupReturningNull_Throws()
    {
        using var cts = new CancellationTokenSource();
        DataBoundaryLookup lookup = (_, _, _) => { cts.Cancel(); return Task.FromResult<string?>(null); };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AgentStartupBoundary.ResolveBuiltinEndpointAsync(
            Options(), new CountingAuthService(), new StubProfileService(), preAcquiredAuth: null, dataBoundary: null,
            lookup, cts.Token));
    }

    private sealed class CountingAuthService : AuthServiceStub
    {
        public const string IssuedToken = "acquired-token";

        public int AcquireCount { get; private set; }
        public Profile? RequestedProfile { get; private set; }
        public string? RequestedTenant { get; private set; }

        public override Task<AuthResult> AcquireTokenSilentlyAsync(
            string[]? scopes, string? tenantId, bool promptOnError = false,
            CancellationToken cancellationToken = default, Profile? profile = null)
        {
            AcquireCount++;
            RequestedProfile = profile;
            RequestedTenant = tenantId;
            return Task.FromResult(new AuthResult
            {
                AccessToken = IssuedToken,
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                Scopes = scopes ?? Array.Empty<string>(),
                TenantId = TenantId,
                CorrelationId = Guid.NewGuid(),
            });
        }
    }

    private sealed class ThrowingAuthService : AuthServiceStub
    {
        public override Task<AuthResult> AcquireTokenSilentlyAsync(
            string[]? scopes, string? tenantId, bool promptOnError = false,
            CancellationToken cancellationToken = default, Profile? profile = null) =>
            throw new InvalidOperationException("no cached account");
    }

    private sealed class CancellingAuthService : AuthServiceStub
    {
        public override Task<AuthResult> AcquireTokenSilentlyAsync(
            string[]? scopes, string? tenantId, bool promptOnError = false,
            CancellationToken cancellationToken = default, Profile? profile = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("the caller's token should have been signalled");
        }
    }

    // MSAL cancels its own token on an internal timeout, so the exception looks identical to a
    // user cancellation apart from which token it names.
    private sealed class InternallyTimingOutAuthService : AuthServiceStub
    {
        public override Task<AuthResult> AcquireTokenSilentlyAsync(
            string[]? scopes, string? tenantId, bool promptOnError = false,
            CancellationToken cancellationToken = default, Profile? profile = null) =>
            throw new OperationCanceledException(new CancellationToken(canceled: true));
    }

    private abstract class AuthServiceStub : IAuthService
    {
        public Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task LogoutAsync(string? userPrincipalName, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public abstract Task<AuthResult> AcquireTokenSilentlyAsync(
            string[]? scopes, string? tenantId, bool promptOnError = false,
            CancellationToken cancellationToken = default, Profile? profile = null);
        public Task<bool> CanAcquireTokenSilentlyAsync(string? tenantId, Profile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    // Signed in to TenantId unless a test overrides it.
    private class StubProfileService : IProfileService
    {
        public Task<IReadOnlyCollection<Profile>> GetAllProfilesAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public virtual Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task SaveProfileAsync(Profile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAndActivateProfileAsync(Profile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> RemoveProfileAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<string?> GetActiveProfileNameAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
        public virtual Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<Profile?>(new Profile { TenantId = TenantId });
        public Task SetActiveProfileNameAsync(string? name, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public List<string> GetProfileKeysForDefault() => throw new NotImplementedException();
        public string? GetProfileValueByKey(Profile? profile, string key) => throw new NotImplementedException();
    }

    private sealed class DifferentProfileService(Profile commandProfile) : StubProfileService
    {
        internal Profile ActiveProfile { get; } = new() { Name = "other", TenantId = "other-tenant" };
        internal int ActiveReads { get; private set; }

        public override Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult<Profile?>(name == commandProfile.Name ? commandProfile : null);

        public override Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
        {
            ActiveReads++;
            return Task.FromResult<Profile?>(ActiveProfile);
        }
    }

    private sealed class SignedOutProfileService : StubProfileService
    {
        public override Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<Profile?>(null);
    }

    private sealed class ThrowingProfileService : StubProfileService
    {
        public override Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("the profile store is unreadable");
    }
}
