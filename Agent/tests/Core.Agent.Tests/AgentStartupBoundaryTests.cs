using Core.Abstractions;
using Core.Agent.AgentMode;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Contract tests for <see cref="AgentStartupBoundary"/>, the seam that acquires the Azure token
/// and resolves the caller tenant's data boundary before the agent is created.
/// </summary>
public class AgentStartupBoundaryTests
{
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
        // Core's resolver catches every exception, so Ctrl+C during the ARM call returns null.
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
        // The resolver's own timeout is not the user cancelling.
        var startup = await StartAsync(new CountingAuthService(), new RecordingLookup(null).Invoke);

        Assert.Null(startup.DataBoundary);
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
    public async Task LookupOrProfileFailure_KeepsTheTokenAndDoesNotBlockRecovery()
    {
        var throwingLookup = await StartAsync(
            new CountingAuthService(), (_, _, _) => throw new InvalidOperationException("ARM is down"));
        var throwingProfile = await StartAsync(
            new CountingAuthService(), new RecordingLookup("EU").Invoke, profiles: new ThrowingProfileService());
        var noAnswer = await StartAsync(new CountingAuthService(), new RecordingLookup(null).Invoke);

        foreach (var startup in new[] { throwingLookup, throwingProfile, noAnswer })
        {
            Assert.Null(startup.DataBoundary);
            Assert.Equal(CountingAuthService.IssuedToken, startup.Auth!.AccessToken);
            Assert.Null(startup.Failure);
        }
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

    private sealed class CountingAuthService : AuthServiceStub
    {
        public const string IssuedToken = "acquired-token";

        public int AcquireCount { get; private set; }

        public override Task<AuthResult> AcquireTokenSilentlyAsync(
            string[]? scopes, string? tenantId, bool promptOnError = false,
            CancellationToken cancellationToken = default, Profile? profile = null)
        {
            AcquireCount++;
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
        public Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default) =>
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
