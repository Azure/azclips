using Core.Abstractions;
using Core.Agent.AgentMode;
using Core.Agent.Providers;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Contract tests for <see cref="AgentStartupBoundary"/>, the seam that acquires the Azure token
/// and probes the caller tenant's data boundary before the agent is created.
///
/// <para>Everything here exists because the caller writes <c>recovery.dataBoundary</c> exactly
/// once, unconditionally, from what this returns. That write is only safe if the seam always
/// returns — so these tests pin the classification of each failure and the fact that none of them
/// escapes as an exception.</para>
/// </summary>
public class AgentStartupBoundaryTests
{
    private const string TenantId = "fake-tenant";

    private static AgentOptions Options(string? provider = "builtin") => new() { Provider = provider! };

    /// <summary>
    /// Runs the seam and turns a violated "never throws" contract into a named assertion failure.
    /// </summary>
    /// <remarks>
    /// Without this, removing one of the seam's catches makes the test red with the escaping
    /// exception itself, which looks the same as the test being broken.
    /// </remarks>
    private static async Task<StartupBoundary> NeverThrowsAsync(
        Func<Task<StartupBoundary>> startup, string invariant)
    {
        try
        {
            return await startup();
        }
        catch (Exception ex)
        {
            Assert.Fail($"{invariant} It threw {ex.GetType().Name}: {ex.Message}");
            throw; // unreachable: Assert.Fail always throws.
        }
    }

    [Fact]
    public async Task StartupBoundary_TokenThrows_RecordsNoToken()
    {
        // A signed-out user is the common case of this path. It has to arrive as a value, because
        // the caller records the outcome before re-raising the actionable "run az login".
        var startup = await AgentStartupBoundary.ResolveAsync(
            Options(),
            new ThrowingAuthService(),
            new StubProfileService(),
            _ => new StubResolver(),
            session: null,
            CancellationToken.None);

        Assert.Equal("no_token", startup.Boundary.Value);
        Assert.Null(startup.Auth);
        Assert.NotNull(startup.Failure);
    }

    [Fact]
    public async Task StartupBoundary_CallerCancels_RecordsCancelled_AndSurfacesTheFailure()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var startup = await AgentStartupBoundary.ResolveAsync(
            Options(),
            new CancellingAuthService(),
            new StubProfileService(),
            _ => new StubResolver(),
            session: null,
            cts.Token);

        Assert.Equal("cancelled", startup.Boundary.Value);
        // Carried out rather than thrown: the caller records the outcome first, then re-raises the
        // original exception with its original stack, so Ctrl+C still exits the way it always did.
        Assert.NotNull(startup.Failure);
        Assert.ThrowsAny<OperationCanceledException>(() => startup.Failure!.Throw());
    }

    [Fact]
    public async Task StartupBoundary_MsalInternalTimeout_RecordsTimeoutToken_NotCancelled()
    {
        // A library cancelling its own internal token is not the user pressing Ctrl+C, and the
        // caller's token is the only thing that separates them.
        using var cts = new CancellationTokenSource();

        var startup = await AgentStartupBoundary.ResolveAsync(
            Options(),
            new InternallyTimingOutAuthService(),
            new StubProfileService(),
            _ => new StubResolver(),
            session: null,
            cts.Token);

        Assert.False(
            cts.IsCancellationRequested,
            "the caller's token must stay unsignalled or this case cannot tell the two apart.");
        Assert.Equal("timeout_token", startup.Boundary.Value);
        Assert.NotNull(startup.Failure);
    }

    [Theory]
    // github is the only provider that legitimately skips the lookup.
    [InlineData("github", "skipped_github")]
    [InlineData("GitHub", "skipped_github")]
    // Everything else that reaches here is a value startup is about to reject. The boundary is
    // recorded first, so a typo must not be filed as a GitHub session that ran.
    [InlineData("gihub", "skipped_unsupported_provider")]
    [InlineData("openai", "skipped_unsupported_provider")]
    [InlineData("", "skipped_unsupported_provider")]
    [InlineData(null, "skipped_unsupported_provider")]
    public async Task StartupBoundary_GithubAndUnsupported_NeverConstructTheResolver(
        string? provider, string expected)
    {
        // Neither provider has an Azure token to look a boundary up with, and asking a github user
        // for an Azure login would be a regression. The counting factory is what proves the branch
        // is taken before anything is constructed rather than after.
        var factory = new CountingResolverFactory();
        var auth = new CountingAuthService();

        var startup = await AgentStartupBoundary.ResolveAsync(
            Options(provider),
            auth,
            new StubProfileService(),
            factory.Create,
            session: null,
            CancellationToken.None);

        Assert.Equal(0, factory.CreateCount);
        Assert.Equal(0, auth.AcquireCount);
        Assert.Equal(expected, startup.Boundary.Value);
        Assert.Null(startup.Auth);
        Assert.Null(startup.Failure);
    }

    [Fact]
    public async Task StartupBoundary_Builtin_PassesTheAcquiredTokenToTheResolver()
    {
        // One MSAL flow serves both the model provider and the lookup. Acquiring again here would
        // build a second IPublicClientApplication over the same on-disk cache, unsynchronized.
        var auth = new CountingAuthService();
        var resolver = new RecordingResolver();

        var startup = await AgentStartupBoundary.ResolveAsync(
            Options(),
            auth,
            new StubProfileService(),
            _ => resolver,
            session: null,
            CancellationToken.None);

        Assert.Equal(CountingAuthService.IssuedToken, resolver.ReceivedToken);
        Assert.Equal(1, auth.AcquireCount);
        Assert.Equal("eu", startup.Boundary.Value);
        Assert.NotNull(startup.Auth);
        Assert.Null(startup.Failure);
    }

    [Fact]
    public async Task Seam_NeverThrows_EvenWhenEveryDependencyIsBroken()
    {
        // The caller's SetTag is unconditional and has no path around it, which is only true while
        // this holds. A throw here does not lose one lookup — it leaves the session with no
        // boundary recorded at all, which is exactly the hole this design removed.
        const string Invariant =
            "the seam must never throw; its caller records recovery.dataBoundary unconditionally from the return value.";

        var brokenAuth = await NeverThrowsAsync(
            () => AgentStartupBoundary.ResolveAsync(
                Options(), new ThrowingAuthService(), new ThrowingProfileService(),
                _ => new StubResolver(), session: null, CancellationToken.None),
            $"{Invariant} Token acquisition threw.");

        var brokenFactory = await NeverThrowsAsync(
            () => AgentStartupBoundary.ResolveAsync(
                Options(), new CountingAuthService(), new ThrowingProfileService(),
                _ => throw new InvalidOperationException("the resolver factory is broken"),
                session: null, CancellationToken.None),
            $"{Invariant} The resolver factory threw.");

        var brokenResolver = await NeverThrowsAsync(
            () => AgentStartupBoundary.ResolveAsync(
                Options(), new CountingAuthService(), new ThrowingProfileService(),
                _ => new ThrowingResolver(), session: null, CancellationToken.None),
            $"{Invariant} The resolver threw.");

        Assert.NotNull(brokenAuth.Failure);
        Assert.NotNull(brokenFactory.Failure);
        Assert.NotNull(brokenResolver.Failure);
    }

    private sealed class CountingResolverFactory
    {
        public int CreateCount { get; private set; }

        public IDataBoundaryResolver Create(IProfileService profileService)
        {
            CreateCount++;
            return new StubResolver();
        }
    }

    private sealed class StubResolver : IDataBoundaryResolver
    {
        public void Dispose() { }

        public Task<DataBoundaryResult> ResolveAsync(string accessToken, CancellationToken ct = default) =>
            Task.FromResult(DataBoundaryResult.FromArm("EU", TenantId));
    }

    private sealed class RecordingResolver : IDataBoundaryResolver
    {
        public string? ReceivedToken { get; private set; }

        public void Dispose() { }

        public Task<DataBoundaryResult> ResolveAsync(string accessToken, CancellationToken ct = default)
        {
            ReceivedToken = accessToken;
            return Task.FromResult(DataBoundaryResult.FromArm("EU", TenantId));
        }
    }

    private sealed class ThrowingResolver : IDataBoundaryResolver
    {
        public void Dispose() { }

        public Task<DataBoundaryResult> ResolveAsync(string accessToken, CancellationToken ct = default) =>
            throw new InvalidOperationException("the resolver is broken");
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

    // The seam only hands this to the factory, so nothing on it is ever called.
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
            Task.FromResult<Profile?>(null);
        public Task SetActiveProfileNameAsync(string? name, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public List<string> GetProfileKeysForDefault() => throw new NotImplementedException();
        public string? GetProfileValueByKey(Profile? profile, string key) => throw new NotImplementedException();
    }

    private sealed class ThrowingProfileService : StubProfileService
    {
        public override Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("the profile store is unreadable");
    }
}
