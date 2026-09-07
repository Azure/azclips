using System.Net;
using System.Text;
using Core.Abstractions;
using Core.Agent.Providers;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Contract tests for <see cref="DataBoundaryResolver"/>.
///
/// <para>The behavior that matters most here is what the resolver does when things go wrong.
/// It sits on the startup path of every AI recovery while nothing yet depends on its answer, so
/// a failure has to degrade to Unknown rather than surface as an exception. Most of these cases
/// exist to pin that down.</para>
///
/// <para>The other half is parsing discipline: an unrecognized value must not be rounded to
/// Global. Once routing exists, that mistake would send an EU tenant's prompts out of the EU.</para>
/// </summary>
public class DataBoundaryResolverTests
{
    private const string TenantId = "fake-tenant";

    private static string BoundaryPayload(string? dataBoundary, string? name = TenantId)
    {
        var boundaryJson = dataBoundary is null ? "null" : $"\"{dataBoundary}\"";
        var nameJson = name is null ? "null" : $"\"{name}\"";
        return $$"""
        {
          "id": "/providers/Microsoft.Resources/dataBoundaries/{{name}}",
          "name": {{nameJson}},
          "properties": { "dataBoundary": {{boundaryJson}}, "provisioningState": "Succeeded" }
        }
        """;
    }

    private static DataBoundaryResolver Build(HttpMessageHandler handler, string? tenantId = TenantId) =>
        new(new FakeAuthService(), new FakeProfileService(tenantId), new HttpClient(handler));

    [Theory]
    [InlineData("EU", DataBoundary.Eu)]
    [InlineData("Global", DataBoundary.Global)]
    // ARM's casing is not contractual, so both cases have to map the same way.
    [InlineData("eu", DataBoundary.Eu)]
    [InlineData("global", DataBoundary.Global)]
    internal async Task Resolve_MapsDocumentedValues(string raw, DataBoundary expected)
    {
        var resolver = Build(new FakeHandler(_ => (HttpStatusCode.OK, BoundaryPayload(raw))));

        var result = await resolver.ResolveAsync();

        Assert.Equal(expected, result.Boundary);
        Assert.Equal(raw, result.RawValue);
        Assert.Equal(TenantId, result.TenantId);
        Assert.Null(result.FailureReason);
    }

    [Theory]
    // Documented today but not a boundary we can route on.
    [InlineData("NotDefined")]
    // Stands in for a value ARM adds after this ships.
    [InlineData("SomeFutureBoundary")]
    public async Task Resolve_UnrecognizedValue_IsUnknownAndKeepsRawValue(string raw)
    {
        var resolver = Build(new FakeHandler(_ => (HttpStatusCode.OK, BoundaryPayload(raw))));

        var result = await resolver.ResolveAsync();

        Assert.Equal(DataBoundary.Unknown, result.Boundary);
        // Retained so telemetry can show which value we failed to recognize.
        Assert.Equal(raw, result.RawValue);
        Assert.Equal("unrecognized_value", result.FailureReason);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "arm_status_401")]
    [InlineData(HttpStatusCode.Forbidden, "arm_status_403")]
    [InlineData(HttpStatusCode.InternalServerError, "arm_status_500")]
    public async Task Resolve_ArmRejects_IsUnknownWithoutThrowing(HttpStatusCode status, string expectedReason)
    {
        var resolver = Build(new FakeHandler(_ => (status, """{"error":{"code":"AuthorizationFailed"}}""")));

        var result = await resolver.ResolveAsync();

        Assert.Equal(DataBoundary.Unknown, result.Boundary);
        Assert.Equal(expectedReason, result.FailureReason);
    }

    [Theory]
    [InlineData("", "empty_response")]
    [InlineData("not json at all", "malformed_response")]
    [InlineData("""{"properties":{}}""", "missing_data_boundary")]
    [InlineData("""{"unexpected":"shape"}""", "missing_data_boundary")]
    public async Task Resolve_UnusableBody_IsUnknownWithoutThrowing(string body, string expectedReason)
    {
        var resolver = Build(new FakeHandler(_ => (HttpStatusCode.OK, body)));

        var result = await resolver.ResolveAsync();

        Assert.Equal(DataBoundary.Unknown, result.Boundary);
        Assert.Equal(expectedReason, result.FailureReason);
    }

    [Fact]
    public async Task Resolve_ResponseForDifferentTenant_IsRejected()
    {
        // ARM echoes the tenant in 'name'. A mismatch means we would be reading some other
        // tenant's boundary, which is worse than having no answer.
        var resolver = Build(new FakeHandler(_ => (HttpStatusCode.OK, BoundaryPayload("EU", name: "someone-else"))));

        var result = await resolver.ResolveAsync();

        Assert.Equal(DataBoundary.Unknown, result.Boundary);
        Assert.Equal("tenant_mismatch", result.FailureReason);
    }

    [Fact]
    public async Task Resolve_NoActiveProfile_SkipsTheCallEntirely()
    {
        var handler = new FakeHandler(_ => (HttpStatusCode.OK, BoundaryPayload("EU")));
        var resolver = Build(handler, tenantId: null);

        var result = await resolver.ResolveAsync();

        Assert.Equal(DataBoundary.Unknown, result.Boundary);
        Assert.Equal("no_active_tenant", result.FailureReason);
        // Signed out is knowable without ARM; spending a round-trip to learn it would add
        // startup latency for an answer we already have.
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Resolve_TokenAcquisitionFails_IsUnknownWithoutThrowing()
    {
        var handler = new FakeHandler(_ => (HttpStatusCode.OK, BoundaryPayload("EU")));
        var resolver = new DataBoundaryResolver(
            new ThrowingAuthService(),
            new FakeProfileService(TenantId),
            new HttpClient(handler));

        var result = await resolver.ResolveAsync();

        Assert.Equal(DataBoundary.Unknown, result.Boundary);
        Assert.StartsWith("token_unavailable", result.FailureReason);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Resolve_TransportFailure_IsUnknownWithoutThrowing()
    {
        var resolver = Build(new ThrowingHandler(new HttpRequestException("connection reset")));

        var result = await resolver.ResolveAsync();

        Assert.Equal(DataBoundary.Unknown, result.Boundary);
        Assert.StartsWith("arm_request_failed", result.FailureReason);
    }

    [Fact]
    public async Task Resolve_SlowArm_TimesOutWithoutThrowing()
    {
        // Hangs until its own token trips, standing in for an ARM call that outlives the
        // resolver's budget. Startup must not be held hostage by it.
        var resolver = Build(new HangingHandler());

        var result = await resolver.ResolveAsync();

        Assert.Equal(DataBoundary.Unknown, result.Boundary);
        Assert.Equal("timeout", result.FailureReason);
    }

    [Fact]
    public async Task Resolve_CallerCancels_Propagates()
    {
        // The one case that must throw: Ctrl+C during startup should exit, not be recorded as
        // a lookup failure.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var resolver = Build(new HangingHandler());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resolver.ResolveAsync(cts.Token));
    }

    [Theory]
    [InlineData(DataBoundary.Eu, "eu")]
    [InlineData(DataBoundary.Global, "global")]
    [InlineData(DataBoundary.Unknown, "unknown")]
    internal void TagValue_IsStableLowerCase(DataBoundary boundary, string expected)
    {
        // Telemetry dimensions are grouped on this, so the casing is part of the contract.
        var result = new DataBoundaryResult(boundary, RawValue: null, TenantId: null, FailureReason: null);

        Assert.Equal(expected, result.TagValue);
    }

    [Fact]
    public void Parse_NullOrEmpty_IsUnknown()
    {
        Assert.Equal(DataBoundary.Unknown, DataBoundaryResult.Parse(null));
        Assert.Equal(DataBoundary.Unknown, DataBoundaryResult.Parse(""));
        Assert.Equal(DataBoundary.Unknown, DataBoundaryResult.Parse("   "));
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> _respond;

        public FakeHandler(Func<HttpRequestMessage, (HttpStatusCode, string)> respond) => _respond = respond;

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var (status, body) = _respond(request);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        private readonly Exception _exception;
        public ThrowingHandler(Exception exception) => _exception = exception;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw _exception;
    }

    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class FakeAuthService : IAuthService
    {
        public Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task LogoutAsync(string? userPrincipalName, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<AuthResult> AcquireTokenSilentlyAsync(string[]? scopes, string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default, Profile? profile = null) =>
            Task.FromResult(new AuthResult
            {
                AccessToken = "fake-token",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                Scopes = scopes ?? Array.Empty<string>(),
                TenantId = TenantId,
                CorrelationId = Guid.NewGuid(),
            });
        public Task<bool> CanAcquireTokenSilentlyAsync(string? tenantId, Profile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class ThrowingAuthService : IAuthService
    {
        public Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task LogoutAsync(string? userPrincipalName, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<AuthResult> AcquireTokenSilentlyAsync(string[]? scopes, string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default, Profile? profile = null) =>
            throw new InvalidOperationException("no cached account");
        public Task<bool> CanAcquireTokenSilentlyAsync(string? tenantId, Profile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakeProfileService : IProfileService
    {
        private readonly Profile? _profile;

        public FakeProfileService(string? tenantId) =>
            _profile = tenantId is null
                ? null
                : new Profile { Name = "default", TenantId = tenantId, SubscriptionId = "sub", CloudName = "AzureCloud" };

        public Task<IReadOnlyCollection<Profile>> GetAllProfilesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Profile>>(_profile is null ? Array.Empty<Profile>() : new[] { _profile });
        public Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(_profile is not null && _profile.Name == name ? _profile : null);
        public Task SaveProfileAsync(Profile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAndActivateProfileAsync(Profile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> RemoveProfileAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<string?> GetActiveProfileNameAsync(CancellationToken cancellationToken = default) => Task.FromResult(_profile?.Name);
        public Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default) => Task.FromResult(_profile);
        public Task SetActiveProfileNameAsync(string? name, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public List<string> GetProfileKeysForDefault() => throw new NotImplementedException();
        public string? GetProfileValueByKey(Profile? profile, string key) => throw new NotImplementedException();
    }
}
