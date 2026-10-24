// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry.Tests;

public sealed class NativeTelemetryCreationTests
{
    [Fact]
    public void Service_RecordsIdentityBeforeChildWorkWithoutParallelHierarchy()
    {
        using var fixture = new Fixture();
        using var sessionScope = fixture.Service.StartIndependentRootActivity("Agent.Session");
        var session = sessionScope?.Activity;
        Assert.NotNull(session);
        Assert.Equal(default, session.ParentSpanId);
        Assert.NotNull(session.GetTagItem(TelemetryTags.InstallationId));
        Assert.NotNull(session.GetTagItem(TelemetryTags.ProductVersion));
        session.DisplayName = "display-only detail";
        using var child = fixture.Service.StartActivity("MsalAuthService.AcquireTokenSilentlyAsync");
        Assert.NotNull(child);
        Assert.False(session.IsStopped);
        Assert.Equal(session.TraceId, child.TraceId);
        Assert.Equal(session.SpanId, child.ParentSpanId);
        Assert.DoesNotContain(child.TagObjects, tag => tag.Key.StartsWith("properties.hierarchy.", StringComparison.Ordinal));
        Assert.Equal("MsalAuthService.AcquireTokenSilentlyAsync", child.OperationName);
    }

    [Fact]
    public void AmbientParenting_UsesActualContextRatherThanInventedMetadata()
    {
        using var fixture = new Fixture();
        using var unknown = fixture.Source.StartActivity("unknown");
        using var child = fixture.Service.StartActivity("Agent.Send");
        Assert.NotNull(unknown);
        Assert.NotNull(child);
        Assert.Equal(unknown.SpanId, child.ParentSpanId);
        Assert.DoesNotContain(child.TagObjects, tag => tag.Key.StartsWith("properties.hierarchy.", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Agent.Session")]
    [InlineData("Agent.Create")]
    [InlineData("tool.get_command")]
    public void ExplicitRoots_KeepTheirNamesAndRejectUnrelatedAmbient(string operationName)
    {
        using var fixture = new Fixture();
        using var ambient = fixture.Service.StartActivity("unrelated");
        using (var scope = fixture.Service.StartIndependentRootActivity(operationName))
        {
            var root = scope?.Activity;
            Assert.NotNull(root);
            Assert.Equal(operationName, root.OperationName);
            Assert.Equal(default, root.ParentSpanId);
            Assert.NotEqual(ambient!.TraceId, root.TraceId);
        }
        Assert.Same(ambient, Activity.Current);
    }

    [Fact]
    public void ActualEdges_DistinguishRepeatedSendsAndNestedTools()
    {
        using var fixture = new Fixture();
        using var sessionScope = fixture.Service.StartIndependentRootActivity("Agent.Session");
        var session = sessionScope?.Activity;
        using (var create = fixture.Service.StartActivity("Agent.Create"))
        using (var lookup = fixture.Service.StartActivity("tool.get_command"))
        {
            Assert.Equal(session!.SpanId, create!.ParentSpanId);
            Assert.Equal(create.SpanId, lookup!.ParentSpanId);
        }
        var sends = new List<ActivitySpanId>();
        var tools = new List<ActivitySpanId>();
        for (var round = 0; round < 2; round++)
        {
            using var turn = fixture.Service.StartActivity("Agent.Turn");
            using var sendScope = fixture.Service.StartActivityWithParent("Agent.Send", turn!.Context);
            var send = sendScope?.Activity;
            using var tool = fixture.Service.StartActivity("tool.get_command");
            using var nested = fixture.Service.StartActivity("MsalAuthService.AcquireTokenSilentlyAsync");
            Assert.Equal(turn.SpanId, send!.ParentSpanId);
            Assert.Equal(send.SpanId, tool!.ParentSpanId);
            Assert.Equal(tool.SpanId, nested!.ParentSpanId);
            sends.Add(send.SpanId);
            tools.Add(tool.SpanId);
        }
        Assert.NotEqual(sends[0], sends[1]);
        Assert.NotEqual(tools[0], tools[1]);
        using var retryTurn = fixture.Service.StartActivity("Agent.Turn");
        using var reprompt = fixture.Service.StartActivity("Agent.Reprompt");
        using var retrySendScope = fixture.Service.StartActivityWithParent("Agent.Send", reprompt!.Context);
        var retrySend = retrySendScope?.Activity;
        Assert.Equal(retryTurn!.SpanId, reprompt.ParentSpanId);
        Assert.Equal(reprompt.SpanId, retrySend!.ParentSpanId);
    }

    [Fact]
    public void OptOut_AppliesToAllCreationPaths()
    {
        using var fixture = new Fixture(enabled: false);
        Assert.Null(fixture.Service.StartRootActivity());
        Assert.Null(fixture.Service.StartActivity("Agent.Send"));
        Assert.Null(fixture.Service.StartActivityWithParent("Agent.Create", default));
        Assert.Null(fixture.Service.StartIndependentRootActivity("Agent.Session"));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ActivityListener _listener;
        internal Fixture(bool enabled = true)
        {
            Source = new ActivitySource("native-creation-" + Guid.NewGuid());
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source == Source,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            };
            ActivitySource.AddActivityListener(_listener);
            Service = new TelemetryService(new Provider(Source), new Configuration(enabled), new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));
        }
        internal ActivitySource Source { get; }
        internal TelemetryService Service { get; }
        public void Dispose()
        {
            Service.Dispose();
            _listener.Dispose();
            Source.Dispose();
        }
    }

    private sealed class Provider(ActivitySource source) : ITelemetryProvider
    {
        public ActivitySource ActivitySource { get; } = source;
        public TracerProvider TracerProvider { get; } = Sdk.CreateTracerProviderBuilder().Build();
    }

    private sealed class Configuration(bool enabled) : IConfigurationService
    {
        public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new() =>
            typeof(T) == typeof(TelemetryOptions)
                ? (T)(object)new TelemetryOptions { Enabled = enabled }
                : defaultValue ?? new T();
        public Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<string, ConfigurationValue>> ListConfigurationWithSourcesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public string GetConfigFilePath() => throw new NotSupportedException();
        public IConfigurationSchema GetSchema() => throw new NotSupportedException();
    }
}
