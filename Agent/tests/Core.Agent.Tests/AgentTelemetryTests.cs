// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Agent.AgentMode;
using Core.Agent.Telemetry;
using Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Core.Agent.Tests;

public class RecoveryTraceContextTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-traceparent")]
    [InlineData("00-tooshort-00f067aa0ba902b7-01")]
    public void Parse_ReturnsDefault_WhenAbsentOrMalformed(string? value)
    {
        // default(ActivityContext) makes the caller fall back to ambient parenting, so a
        // missing or broken handoff costs the parent edge and nothing else.
        Assert.Equal(default, RecoveryTraceContext.Parse(value));
    }


    [Fact]
    public void EnvVar_UsesTheSharedLiteral()
    {
        // Keep this literal pinned here; Cli.Tests asserts the counterpart on the CLI side
        // because neither test project can see both internals.
        Assert.Equal("AZCLIPS_RECOVERY_TRACEPARENT", RecoveryTraceContext.EnvVar);
    }

    [Fact]
    public void Parse_ExtractsTraceAndSpan_WhenWellFormed()
    {
        var ctx = RecoveryTraceContext.Parse(
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", ctx.TraceId.ToString());
        Assert.Equal("00f067aa0ba902b7", ctx.SpanId.ToString());
    }
}

// OpenTelemetry listeners are process-global and AgentTracing.Source is static, so the
// enabled/disabled assertions must not overlap any other test that touches this source.
[CollectionDefinition("AgentTelemetry", DisableParallelization = true)]
public class AgentTelemetryCollection { }

[Collection("AgentTelemetry")]
public class AgentTelemetryProviderTests
{
    [Fact]
    public void ActivitySource_IsNamedForTheCliClient()
    {
        // FileExporter writes service.name from ActivitySource.Name, and TraceUploader
        // rebuilds parent/child edges only within a service-name group. Drift from
        // CLITelemetryProvider reparents every agent span to a random id on upload.
        var provider = new AgentTelemetryProvider(
            [], new NoopSender(), new StubConfiguration(telemetryEnabled: true));

        try
        {
            Assert.Equal(ClientType.CLI.ToString(), provider.ActivitySource.Name);
            Assert.Equal(AgentTracing.SourceName, provider.ActivitySource.Name);
        }
        finally
        {
            // Prevent this test's process-global listener from leaking into later tests.
            provider.TracerProvider.Dispose();
        }
    }

    [Fact]
    public void TracerProvider_ListensToAgentTracingSource_WhenTelemetryIsEnabled()
    {
        var provider = new AgentTelemetryProvider(
            [], new NoopSender(), new StubConfiguration(telemetryEnabled: true));

        try
        {
            using var activity = AgentTracing.Source.StartActivity("Agent.Session");
            Assert.NotNull(activity);
        }
        finally
        {
            // Prevent this test's process-global listener from leaking into later tests.
            provider.TracerProvider.Dispose();
        }
    }

    [Fact]
    public void TracerProvider_DoesNotListen_WhenTelemetryIsDisabled()
    {
        var provider = new AgentTelemetryProvider(
            [], new NoopSender(), new StubConfiguration(telemetryEnabled: false));

        // AgentTracing.Source bypasses TelemetryService's per-call gate, so the opt-out has
        // to be enforced by not building a listener at all.
        using var activity = AgentTracing.Source.StartActivity("Agent.Session");
        Assert.Null(activity);
    }

    [Theory]
    [InlineData("ok", 0, ActivityStatusCode.Ok, null)]
    [InlineData("cancelled", 130, ActivityStatusCode.Unset, null)]
    [InlineData("error", 1, ActivityStatusCode.Error, nameof(InvalidOperationException))]
    public async Task RunSessionAsync_ExportsTerminalOutcome_BeforeDisposingServices(
        string expectedOutcome, int expectedExitCode, ActivityStatusCode expectedStatus, string? expectedFailureReason)
    {
        const string sensitiveText = "test-only private error detail";
        var order = new List<string>();
        var sender = new SnapshotSender(order);
        var provider = new AgentTelemetryProvider(
            [
                new DefaultActivityNameFilterProcessor(new DefaultTelemetryActivityNameFilter()),
                new DefaultTelemetryPropertyFilterProcessor(new DefaultTelemetryPropertyFilter()),
            ],
            sender, new StubConfiguration(telemetryEnabled: true));
        bool sessionWasActive = false;
        int disposeCalls = 0;

        try
        {
            var exitCode = await AgentModeEntry.RunSessionAsync(
                () => provider,
                async session =>
                {
                    sessionWasActive = session is not null &&
                        ReferenceEquals(session, Activity.Current) &&
                        session.Duration == TimeSpan.Zero && sender.Exported.Count == 0;
                    order.Add("agent-ran");
                    await Task.Yield();
                    if (expectedExitCode == 130) throw new OperationCanceledException(sensitiveText);
                    if (expectedExitCode == 1) throw new InvalidOperationException(sensitiveText);
                },
                () =>
                {
                    disposeCalls++;
                    order.Add("services-disposing");
                    provider.TracerProvider.Dispose();
                });

            Assert.Equal(expectedExitCode, exitCode);
            Assert.True(sessionWasActive);
            Assert.Equal(1, disposeCalls);
            Assert.Equal(new[] { "agent-ran", "session-exported", "services-disposing" }, order);
            var exported = Assert.Single(sender.Exported);
            Assert.Equal("Agent.Session", exported.Name);
            Assert.Equal(expectedOutcome, exported.Tags.GetValueOrDefault(TelemetryTags.RecoveryOutcome));
            // A host failure is classified by exception type, the same way a model failure is by SDK
            // error type — without it the dashboard sees "error" and cannot tell our bug from theirs.
            // Success and cancellation must leave it absent so the column stays a failure signal.
            Assert.Equal(expectedFailureReason, exported.Tags.GetValueOrDefault(TelemetryTags.RecoveryFailureReason));
            Assert.Equal(expectedStatus, exported.Status);
            Assert.Equal("azclips-agent", exported.Tags.GetValueOrDefault(TelemetryTags.ProcessName));
            Assert.Null(exported.StatusDescription);
            Assert.Empty(exported.Events);
            Assert.DoesNotContain(exported.Tags.Values,
                value => value is string text && text.Contains(sensitiveText, StringComparison.Ordinal));
        }
        finally
        {
            provider.TracerProvider.Dispose();
            provider.ActivitySource.Dispose();
        }
    }

    [Fact]
    internal async Task RunSessionAsync_RecordsError_WhenSessionEndsOnTerminalSdkFailure()
    {
        // The chat loop swallows the SDK's cancellation so an interactive user can take another
        // turn, then re-raises it as AgentSessionFailedException once the loop ends on a failed
        // turn. Without that the loop returns normally and this session exports ok / exit 0 —
        // a run the user just watched fail would count as a success in every metric.
        const string sensitiveText = "test-only private error detail";
        var sender = new SnapshotSender([]);
        var provider = new AgentTelemetryProvider(
            [
                new DefaultActivityNameFilterProcessor(new DefaultTelemetryActivityNameFilter()),
                new DefaultTelemetryPropertyFilterProcessor(new DefaultTelemetryPropertyFilter()),
            ],
            sender, new StubConfiguration(telemetryEnabled: true));

        try
        {
            var exitCode = await AgentModeEntry.RunSessionAsync(
                () => provider,
                _ => throw new AgentSessionFailedException("rate_limit", sensitiveText),
                () => provider.TracerProvider.Dispose());

            Assert.Equal(1, exitCode);
            var exported = Assert.Single(sender.Exported);
            Assert.Equal("error", exported.Tags.GetValueOrDefault(TelemetryTags.RecoveryOutcome));
            // The classification is the point: "error" alone cannot tell throttling from an expired
            // credential, so it says nothing about what to fix.
            Assert.Equal("rate_limit", exported.Tags.GetValueOrDefault(TelemetryTags.RecoveryFailureReason));
            Assert.Equal(ActivityStatusCode.Error, exported.Status);
            // The failure text came from the model provider and can echo user input, so it must not
            // ride out on the span even though the user already saw it on screen.
            Assert.DoesNotContain(exported.Tags.Values,
                value => value is string text && text.Contains(sensitiveText, StringComparison.Ordinal));
            Assert.Null(exported.StatusDescription);
        }
        finally
        {
            provider.TracerProvider.Dispose();
            provider.ActivitySource.Dispose();
        }
    }

    [Fact]
    internal void ThrowIfSessionEndedInError_ConvertsTerminalLastError_IntoSessionFailure()
    {
        // The conversion itself, which the RunSessionAsync test above cannot reach: that one injects
        // the exception directly and would still pass if this step were deleted.
        var lastError = new SessionErrored("rate_limit", "quota exceeded", ErrorCode: null, StatusCode: 429, ProviderCallId: null, At: DateTimeOffset.UtcNow);

        var ex = Assert.Throws<AgentSessionFailedException>(
            () => AgentModeEntry.ThrowIfSessionEndedInError(lastError, CancellationToken.None));

        Assert.Equal("rate_limit", ex.ErrorType);
    }

    [Fact]
    internal void ThrowIfSessionEndedInError_DoesNotThrow_WhenTheLastTurnSucceeded()
    {
        // LastError is per-turn, so a session whose final turn succeeded has none even if an earlier
        // turn failed and the user recovered. That session ends ok.
        AgentModeEntry.ThrowIfSessionEndedInError(terminalError: null, CancellationToken.None);
    }

    [Fact]
    internal void ThrowIfSessionEndedInError_PropagatesCancellation_WhenTheLoopExitedOnTheToken()
    {
        // The chat loops exit by observing the token rather than by throwing, so without this a
        // cancellation arriving after a send completed would return normally and export ok / 0.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => AgentModeEntry.ThrowIfSessionEndedInError(terminalError: null, cts.Token));
    }

    [Fact]
    internal void ThrowIfSessionEndedInError_PrefersTheFailure_WhenBothHappened()
    {
        // A user who stops after a turn failed is still reporting a failed session, and the failure
        // carries a reason while the cancellation does not.
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var lastError = new SessionErrored("auth_failed", "token expired", ErrorCode: null, StatusCode: 401, ProviderCallId: null, At: DateTimeOffset.UtcNow);

        var ex = Assert.Throws<AgentSessionFailedException>(
            () => AgentModeEntry.ThrowIfSessionEndedInError(lastError, cts.Token));

        Assert.Equal("auth_failed", ex.ErrorType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(130)]
    [InlineData(1)]
    public async Task RunSessionAsync_PreservesExitCode_WithoutTelemetry(int expectedExitCode)
    {
        var sender = new SnapshotSender([]);
        var provider = new AgentTelemetryProvider(
            [], sender, new StubConfiguration(telemetryEnabled: false));
        using var source = provider.ActivitySource;
        bool receivedNullSession = false;
        bool disposed = false;

        var exitCode = await AgentModeEntry.RunSessionAsync(
            () => provider,
            session =>
            {
                receivedNullSession = session is null;
                return expectedExitCode switch
                {
                    130 => Task.FromCanceled(new CancellationToken(canceled: true)),
                    1 => Task.FromException(new InvalidOperationException("test-only failure")),
                    _ => Task.CompletedTask,
                };
            },
            () => disposed = true);

        Assert.Equal(expectedExitCode, exitCode);
        Assert.True(receivedNullSession);
        Assert.True(disposed);
        Assert.Empty(sender.Exported);
    }

    [Fact]
    public async Task RunSessionAsync_DisposesServices_WhenTelemetryResolutionFails()
    {
        bool agentRan = false;
        bool disposed = false;

        var exitCode = await AgentModeEntry.RunSessionAsync(
            () => throw new InvalidOperationException("test-only provider failure"),
            _ =>
            {
                agentRan = true;
                return Task.CompletedTask;
            },
            () => disposed = true);

        Assert.Equal(1, exitCode);
        Assert.False(agentRan);
        Assert.True(disposed);
    }

    private sealed record SessionSnapshot(
        string Name, ActivityStatusCode Status, string? StatusDescription,
        Dictionary<string, object?> Tags, ActivityEvent[] Events);

    private sealed class SnapshotSender(List<string> order) : ITelemetrySender
    {
        public List<SessionSnapshot> Exported { get; } = [];

        public void ConfigureExporter(TracerProviderBuilder builder) =>
            builder.AddProcessor(new SimpleActivityExportProcessor(new SnapshotExporter(this, order)));

        private sealed class SnapshotExporter(SnapshotSender sender, List<string> order) : BaseExporter<Activity>
        {
            public override ExportResult Export(in Batch<Activity> batch)
            {
                foreach (var activity in batch)
                {
                    // Snapshot at export: inspecting the mutable Activity later could hide late tagging.
                    sender.Exported.Add(new SessionSnapshot(
                        activity.OperationName, activity.Status, activity.StatusDescription,
                        activity.TagObjects.ToDictionary(tag => tag.Key, tag => tag.Value),
                        activity.Events.ToArray()));
                    order.Add("session-exported");
                }
                return ExportResult.Success;
            }
        }
    }

    private sealed class NoopSender : ITelemetrySender
    {
        public void ConfigureExporter(TracerProviderBuilder builder) { }
    }

    /// <summary>
    /// IConfigurationService stub whose GetConfiguration&lt;TelemetryOptions&gt;() reports a
    /// fixed Enabled value. Mirrors ToggleableConfigurationService in
    /// tests/Core.Telemetry.Tests/TelemetryServiceEnabledGateTests.cs.
    /// </summary>
    private sealed class StubConfiguration : IConfigurationService
    {
        private readonly bool _telemetryEnabled;

        public StubConfiguration(bool telemetryEnabled) => _telemetryEnabled = telemetryEnabled;

        public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new()
        {
            if (typeof(T) == typeof(TelemetryOptions))
            {
                return (T)(object)new TelemetryOptions { Enabled = _telemetryEnabled };
            }

            return defaultValue ?? new T();
        }

        public Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public string GetConfigFilePath() => throw new NotSupportedException();
        public IConfigurationSchema GetSchema() => throw new NotSupportedException();
    }
}

[Collection("AgentTelemetry")]
public class AgentSpanNestingTests
{
    [Fact]
    public void ChildSpans_NestUnderTheSessionSpan()
    {
        // Spans started inside the session's scope become its children through ambient
        // Activity.Current, and the whole subtree inherits the CLI's recovery trace id.
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == AgentTracing.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = stopped.Add,
        };
        ActivitySource.AddActivityListener(listener);

        var parent = RecoveryTraceContext.Parse(
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

        using (var sessionSpan = AgentTracing.Source.StartActivity(
                   "Agent.Session", ActivityKind.Server, parent))
        {
            Assert.NotNull(sessionSpan);
            using (AgentTracing.Source.StartActivity("Agent.Create")) { }
            using (var turnSpan = AgentTracing.Source.StartActivity("Agent.Turn"))
            {
                Assert.NotNull(turnSpan);
                using (AgentTracing.Source.StartActivity("Agent.Send")) { }
            }
        }

        var session = stopped.Single(a => a.OperationName == "Agent.Session");
        var create = stopped.Single(a => a.OperationName == "Agent.Create");
        var turn = stopped.Single(a => a.OperationName == "Agent.Turn");
        var send = stopped.Single(a => a.OperationName == "Agent.Send");

        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", session.TraceId.ToString());
        Assert.Equal("00f067aa0ba902b7", session.ParentSpanId.ToString());
        Assert.Equal(session.TraceId, send.TraceId);

        Assert.Equal(session.SpanId, create.ParentSpanId);
        Assert.Equal(session.SpanId, turn.ParentSpanId);
        Assert.Equal(turn.SpanId, send.ParentSpanId);
    }
}


