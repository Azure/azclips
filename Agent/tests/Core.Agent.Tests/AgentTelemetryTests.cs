// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
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
        public Task<IReadOnlyDictionary<string, ConfigurationValue>> ListConfigurationWithSourcesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
