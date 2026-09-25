using System.Diagnostics;
using System.Net;
using System.Text;
using Core.Abstractions;
using Core.Agent.Providers;
using Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;
using DataBoundaryResolver = Core.Agent.Providers.DataBoundaryResolver;

namespace Core.Agent.Tests;

/// <summary>
/// Contract tests for <see cref="DataBoundaryResolver"/> and the value it produces.
///
/// <para>The behavior that matters most here is what the resolver does when things go wrong.
/// It sits on the startup path of every AI recovery while nothing yet depends on its answer, and
/// its caller records whatever comes back as the session's one boundary value — so a failure has
/// to arrive as a classified value rather than as an exception. Most of these cases exist to pin
/// that down.</para>
///
/// <para>The other half is export discipline: ARM's string is the only untrusted input in the
/// vocabulary, and it lands in a telemetry dimension.</para>
/// </summary>
[Collection("AgentTelemetry")]
public class DataBoundaryResolverTests
{
    private const string TenantId = "fake-tenant";
    private const string CallerToken = "caller-supplied-token";

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
        new(new FakeProfileService(tenantId), new HttpClient(handler));

    [Theory]
    [InlineData(HttpStatusCode.OK, "eu")]
    [InlineData(HttpStatusCode.Forbidden, "arm_status_403")]
    public async Task Resolve_ExportsBoundaryOutcomeAndElapsedAfterFilters(
        HttpStatusCode status, string expectedOutcome)
    {
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(AgentTracing.SourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddProcessor(new DefaultActivityNameFilterProcessor(new DefaultTelemetryActivityNameFilter()))
            .AddProcessor(new DefaultTelemetryPropertyFilterProcessor(new DefaultTelemetryPropertyFilter()))
            .AddProcessor(new SimpleActivityExportProcessor(new BoundaryExporter(exported)))
            .Build();
        using var http = new HttpClient(new DelayedTelemetryHandler(status));
        using var resolver = new DataBoundaryResolver(new FakeProfileService(TenantId), http);
        ActivitySpanId sessionId;
        using (var session = AgentTracing.Source.StartActivity("Agent.Session"))
        {
            Assert.NotNull(session);
            sessionId = session.SpanId;
            var result = await resolver.ResolveAsync(CallerToken);
            Assert.Equal(expectedOutcome, result.Value);
        }

        var boundary = Assert.Single(exported, activity => activity.OperationName == "Agent.DataBoundary");
        Assert.Equal(sessionId, boundary.ParentSpanId);
        Assert.Equal(expectedOutcome, boundary.GetTagItem(TelemetryTags.RecoveryDataBoundary));
        var elapsed = Assert.IsType<long>(boundary.GetTagItem(TelemetryTags.RecoveryDataBoundaryElapsedMs));
        Assert.InRange(elapsed, 15L, 5_000L);
        Assert.Single(exported, activity => activity.OperationName == "Agent.Session");
    }

    /// <summary>
    /// Runs a lookup and turns a violated "never throws" contract into a named assertion failure.
    /// </summary>
    /// <remarks>
    /// Without this, narrowing one of the resolver's catches makes the test red with the escaping
    /// exception itself, which looks the same as the test being broken.
    /// </remarks>
    private static async Task<DataBoundaryResult> NeverThrowsAsync(
        Func<Task<DataBoundaryResult>> lookup, string invariant)
    {
        try
        {
            return await lookup();
        }
        catch (Exception ex)
        {
            Assert.Fail($"{invariant} It threw {ex.GetType().Name}: {ex.Message}");
            throw; // unreachable: Assert.Fail always throws.
        }
    }

    [Theory]
    [InlineData("EU", "eu")]
    [InlineData("Global", "global")]
    // ARM's casing is not contractual, so both cases have to map the same way.
    [InlineData("eu", "eu")]
    [InlineData("global", "global")]
    public async Task Resolve_MapsDocumentedValues(string raw, string expected)
    {
        using var resolver = Build(new FakeHandler(_ => (HttpStatusCode.OK, BoundaryPayload(raw))));

        var result = await resolver.ResolveAsync(CallerToken);

        Assert.Equal(expected, result.Value);
        Assert.Equal(TenantId, result.TenantId);
    }

    [Theory]
    // Documented but not a boundary we route on today.
    [InlineData("NotDefined", "notdefined")]
    // Stands in for a value ARM adds after this ships.
    [InlineData("SomeFutureBoundary", "somefutureboundary")]
    public async Task Resolve_UnfamiliarValue_PassesThroughLowercased(string raw, string expected)
    {
        // Discovering a value we do not recognize is the point of an observation-only probe, so an
        // unfamiliar answer is reported as itself rather than collapsed into a failure bucket.
        using var resolver = Build(new FakeHandler(_ => (HttpStatusCode.OK, BoundaryPayload(raw))));

        var result = await resolver.ResolveAsync(CallerToken);

        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "arm_status_401")]
    [InlineData(HttpStatusCode.Forbidden, "arm_status_403")]
    // ARM throttles with 429. It is the most likely transient failure here and the one the
    // deferred retry (see DataBoundaryResolver) exists for, so pin today's degrade behaviour
    // before that changes.
    [InlineData(HttpStatusCode.TooManyRequests, "arm_status_429")]
    [InlineData(HttpStatusCode.InternalServerError, "arm_status_500")]
    public async Task Resolve_ArmRejects_IsClassifiedWithoutThrowing(HttpStatusCode status, string expected)
    {
        using var resolver = Build(new FakeHandler(_ => (status, """{"error":{"code":"AuthorizationFailed"}}""")));

        var result = await resolver.ResolveAsync(CallerToken);

        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("""{"properties":{}}""")]
    [InlineData("""{"unexpected":"shape"}""")]
    // A 200 whose body is well-formed JSON but not the documented object. These must classify as a
    // response we could not read, not as a transport failure, or the telemetry blames the wrong layer.
    [InlineData("[]")]
    [InlineData("123")]
    [InlineData("""{"properties":null}""")]
    [InlineData("""{"properties":[]}""")]
    public async Task Resolve_UnusableBody_IsUnreadableWithoutThrowing(string body)
    {
        using var resolver = Build(new FakeHandler(_ => (HttpStatusCode.OK, body)));

        var result = await resolver.ResolveAsync(CallerToken);

        Assert.Equal("unreadable", result.Value);
    }

    [Fact]
    public async Task Resolve_ResponseForDifferentTenant_IsRejected()
    {
        // ARM echoes the tenant in 'name'. A mismatch means we would be reading some other
        // tenant's boundary, which is worse than having no answer.
        using var resolver = Build(new FakeHandler(_ => (HttpStatusCode.OK, BoundaryPayload("EU", name: "someone-else"))));

        var result = await resolver.ResolveAsync(CallerToken);

        Assert.Equal("tenant_mismatch", result.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingTenantEcho_IsUnconfirmed_NotMismatch(string? name)
    {
        // Both disqualify the answer, but they are different events. A wrong echo means ARM
        // answered about somebody else; a missing one means the answer proves nothing either way.
        // Reporting the second as the first sends triage looking for a tenant mix-up that never
        // happened.
        using var resolver = Build(new FakeHandler(_ => (HttpStatusCode.OK, BoundaryPayload("EU", name: name))));

        var result = await resolver.ResolveAsync(CallerToken);

        Assert.Equal("tenant_unconfirmed", result.Value);
    }

    [Fact]
    public async Task Resolve_CallsTheDocumentedTenantScopedArmEndpoint()
    {
        // Every other test accepts any request the resolver makes, so a wrong verb, a stale api
        // version, or a subscription prefix slipped into the path would leave them all green.
        HttpMethod? method = null;
        Uri? uri = null;
        var handler = new FakeHandler(request =>
        {
            method = request.Method;
            uri = request.RequestUri;
            return (HttpStatusCode.OK, BoundaryPayload("EU"));
        });
        using var resolver = Build(handler);

        await resolver.ResolveAsync(CallerToken);

        Assert.Equal(HttpMethod.Get, method);
        Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Resources/dataBoundaries/default?api-version=2024-08-01",
            uri?.ToString());
    }

    [Fact]
    public async Task Resolve_SendsTheCallerSuppliedTokenAsTheBearer()
    {
        // The resolver never acquires a token. Its caller already holds an ARM-audience one, and
        // acquiring a second would repeat the whole MSAL flow — a fresh IPublicClientApplication
        // plus another on-disk cache registration — on the recovery startup path.
        string? sentToken = null;
        var handler = new FakeHandler(request =>
        {
            sentToken = request.Headers.Authorization?.Parameter;
            return (HttpStatusCode.OK, BoundaryPayload("EU"));
        });
        using var resolver = Build(handler);

        var result = await resolver.ResolveAsync(CallerToken);

        Assert.Equal("eu", result.Value);
        Assert.Equal(CallerToken, sentToken);
    }

    [Fact]
    public async Task Resolve_WithoutAToken_SkipsTheCallEntirely()
    {
        var handler = new FakeHandler(_ => (HttpStatusCode.OK, BoundaryPayload("EU")));
        using var resolver = Build(handler);

        var result = await resolver.ResolveAsync(string.Empty);

        Assert.Equal("no_token", result.Value);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Resolve_NoActiveProfile_SkipsTheCallEntirely()
    {
        var handler = new FakeHandler(_ => (HttpStatusCode.OK, BoundaryPayload("EU")));
        using var resolver = Build(handler, tenantId: null);

        var result = await resolver.ResolveAsync(CallerToken);

        Assert.Equal("no_tenant", result.Value);
        // Signed out is knowable without ARM; spending a round-trip to learn it would add
        // startup latency for an answer we already have.
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Resolve_TransportFailure_IsClassifiedWithoutThrowing()
    {
        using var resolver = Build(new ThrowingHandler(new HttpRequestException("connection reset")));

        var result = await resolver.ResolveAsync(CallerToken);

        Assert.Equal("arm_failed", result.Value);
    }

    [Fact]
    public async Task Resolve_SlowArm_TimesOutWithoutThrowing()
    {
        // Hangs until its own token trips, standing in for an ARM call that outlives the
        // resolver's budget. Startup must not be held hostage by it.
        using var resolver = Build(new HangingHandler());

        var result = await resolver.ResolveAsync(CallerToken);

        Assert.Equal("timeout_arm", result.Value);
    }

    [Fact]
    public async Task Resolve_CallerCancels_Propagates()
    {
        // The one case that must throw: Ctrl+C during startup should exit, not be recorded as
        // a lookup failure. The seam above turns it into `cancelled`.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var resolver = Build(new HangingHandler());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resolver.ResolveAsync(CallerToken, cts.Token));
    }

    [Fact]
    public async Task Resolve_WhenEveryDependencyThrows_StillReturnsAResult()
    {
        // The caller writes recovery.dataBoundary unconditionally from this return value, so an
        // escaping exception does not merely lose a lookup — it leaves the session with no
        // boundary recorded at all. Each dependency is broken with an exception type the
        // classifying catches are deliberately NOT written for, so narrowing one shows up here
        // instead of being absorbed by a type test that happens to match.
        const string Invariant = "the resolver must classify every failure rather than throw;";

        using var profileBroken = new DataBoundaryResolver(
            new ThrowingProfileService(),
            new HttpClient(new FakeHandler(_ => (HttpStatusCode.OK, BoundaryPayload("EU")))));
        using var armBroken = Build(new ThrowingHandler(new InvalidOperationException("the handler is broken")));

        var fromProfile = await NeverThrowsAsync(
            () => profileBroken.ResolveAsync(CallerToken), $"{Invariant} the profile read threw.");
        var fromArm = await NeverThrowsAsync(
            () => armBroken.ResolveAsync(CallerToken), $"{Invariant} the ARM call threw.");

        Assert.Equal("profile_unavailable", fromProfile.Value);
        Assert.Equal("arm_failed", fromArm.Value);
    }

    [Fact]
    public async Task Resolve_SlowProfileRead_TimesOutWithinTheLookupBudget()
    {
        // The budget has to start before the profile read, not after it. A profile service blocked
        // on file I/O or its own gate would otherwise stall recovery startup past the ceiling this
        // lookup advertises, and no ARM call would ever be attempted to hit the later timeout.
        var handler = new FakeHandler(_ => (HttpStatusCode.OK, BoundaryPayload("EU")));
        using var resolver = new DataBoundaryResolver(new HangingProfileService(), new HttpClient(handler));

        // A hard ceiling at four times the budget. It exists so that an unbounded profile read
        // ends as a named assertion rather than hanging the run.
        var ceiling = DataBoundaryResolver.LookupTimeout * 4;
        using var hard = new CancellationTokenSource(ceiling);

        var started = Stopwatch.StartNew();
        var result = await NeverThrowsAsync(
            () => resolver.ResolveAsync(CallerToken, hard.Token),
            $"the lookup budget did not bound the profile read; the test's hard ceiling ({ceiling}) fired first.");
        started.Stop();

        Assert.Equal("timeout_profile", result.Value);
        Assert.True(
            started.Elapsed < DataBoundaryResolver.LookupTimeout + TimeSpan.FromSeconds(3),
            $"the lookup budget did not bound the profile read; took {started.Elapsed}");
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Resolver_DisposesOnlyTheClientItCreated()
    {
        // An injected client belongs to whoever injected it and outlives any one lookup. Disposing
        // it would take that owner's connection pool down with it, and the failure would surface
        // far from here as an unexplained ObjectDisposedException.
        var handler = new DisposeTrackingHandler(_ => (HttpStatusCode.OK, BoundaryPayload("EU")));
        using var injected = new HttpClient(handler);

        using (var borrower = new DataBoundaryResolver(new FakeProfileService(TenantId), injected))
        {
            Assert.NotNull(borrower);
        }

        Assert.False(handler.Disposed, "the resolver disposed a client it did not create.");

        // Still usable, which is the part the owner actually depends on.
        using var next = new DataBoundaryResolver(new FakeProfileService(TenantId), injected);
        var result = await next.ResolveAsync(CallerToken);
        Assert.Equal("eu", result.Value);

        // A client it created is its own to close, and closing twice must stay safe.
        var owner = new DataBoundaryResolver(new FakeProfileService(TenantId));
        owner.Dispose();
        owner.Dispose();
    }

    [Theory]
    [InlineData("EU", "eu")]
    [InlineData("Global", "global")]
    // Unfamiliar but well-shaped: passed through, because recognising it is not this check's job.
    [InlineData("some-future_boundary9", "some-future_boundary9")]
    // Exactly at the ceiling, and one past it.
    [InlineData("abcdefghijabcdefghijabcdefghijab", "abcdefghijabcdefghijabcdefghijab")]
    [InlineData("abcdefghijabcdefghijabcdefghijabc", "unexpected_value")]
    // Shapes we will not put in a telemetry dimension: separators, whitespace, markup, newlines,
    // and non-ASCII all widen the dimension's cardinality or carry content into it.
    [InlineData("eu global", "unexpected_value")]
    [InlineData("eu,global", "unexpected_value")]
    [InlineData("<script>", "unexpected_value")]
    [InlineData("eu\nglobal", "unexpected_value")]
    [InlineData("欧盟", "unexpected_value")]
    [InlineData(" eu", "unexpected_value")]
    [InlineData("", "unexpected_value")]
    [InlineData(null, "unexpected_value")]
    public void FromArm_AdmitsOnlyPlausibleWireTokens(string? raw, string expected)
    {
        // ARM's string is the only untrusted value in this vocabulary, so it is the only one that
        // is sanitised — the enum's wire forms and arm_status_<code> are built here and trusted by
        // construction. What is rejected is shape, not meaning: an unbounded remote string in a
        // telemetry dimension is both a cardinality problem and a content-injection path.
        Assert.Equal(expected, DataBoundaryResult.FromArm(raw, TenantId).Value);
    }

    [Fact]
    public void EveryUnavailableReason_HasADistinctWireForm()
    {
        // The vocabulary is closed by the compiler — NoAnswer() takes an enum, so no call site can
        // interpolate an exception type or a message into this dimension. What the compiler cannot
        // catch is a new enum member with no wire form (Wire() throws) or one that duplicates an
        // existing value, which would silently merge two causes into one bucket.
        var seen = new Dictionary<string, DataBoundaryUnavailable>();

        foreach (var reason in Enum.GetValues<DataBoundaryUnavailable>())
        {
            var wire = DataBoundaryResult.NoAnswer(reason).Value;

            Assert.False(string.IsNullOrWhiteSpace(wire), $"{reason} has no wire form.");
            Assert.Matches("^[a-z0-9_]+$", wire);
            Assert.False(
                seen.TryGetValue(wire, out var owner),
                $"{reason} and {owner} both emit '{wire}'.");
            seen[wire] = reason;
        }
    }

    private sealed class BoundaryExporter(List<Activity> exported) : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
                exported.Add(activity);
            return ExportResult.Success;
        }
    }

    private sealed class DelayedTelemetryHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(30, cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(BoundaryPayload("EU"), Encoding.UTF8, "application/json"),
            };
        }
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

    // HttpClient.Dispose() disposes its handler, which is the only externally observable proof of
    // whether the resolver disposed a client it was handed.
    private sealed class DisposeTrackingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> _respond;

        public DisposeTrackingHandler(Func<HttpRequestMessage, (HttpStatusCode, string)> respond) => _respond = respond;

        public bool Disposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var (status, body) = _respond(request);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
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

    private sealed class HangingProfileService : ProfileServiceStub
    {
        // Never completes on its own — stands in for a profile read blocked on file I/O or on the
        // service's own gate. Only the caller's token can end it.
        public override Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default) =>
            Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith<Profile?>(
                _ => null, cancellationToken, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    private sealed class ThrowingProfileService : ProfileServiceStub
    {
        public override Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("the profile store is unreadable");
    }

    private sealed class FakeProfileService : ProfileServiceStub
    {
        private readonly Profile? _profile;

        public FakeProfileService(string? tenantId) =>
            _profile = tenantId is null
                ? null
                : new Profile { Name = "default", TenantId = tenantId, SubscriptionId = "sub", CloudName = "AzureCloud" };

        public override Task<IReadOnlyCollection<Profile>> GetAllProfilesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Profile>>(_profile is null ? Array.Empty<Profile>() : new[] { _profile });
        public override Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(_profile is not null && _profile.Name == name ? _profile : null);
        public override Task<string?> GetActiveProfileNameAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_profile?.Name);
        public override Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_profile);
    }

    // Only GetActiveProfileAsync is on the resolver's path; the rest of IProfileService is here so
    // each fake can say what it differs in and nothing else.
    private abstract class ProfileServiceStub : IProfileService
    {
        public virtual Task<IReadOnlyCollection<Profile>> GetAllProfilesAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public virtual Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public virtual Task SaveProfileAsync(Profile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public virtual Task SaveAndActivateProfileAsync(Profile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public virtual Task<bool> RemoveProfileAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public virtual Task<string?> GetActiveProfileNameAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
        public abstract Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default);
        public virtual Task SetActiveProfileNameAsync(string? name, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public virtual List<string> GetProfileKeysForDefault() => throw new NotImplementedException();
        public virtual string? GetProfileValueByKey(Profile? profile, string key) => throw new NotImplementedException();
    }
}
