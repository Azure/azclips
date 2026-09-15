// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry.Tests;

public sealed class SourceHierarchyTests
{
    [Fact]
    public void TelemetryService_RecordsIdentityBeforeChildWork()
    {
        using var fixture = new Fixture();
        using var recovery = fixture.Service.StartRootActivity(name: "CLI.Activity.AiRecovery");
        Assert.NotNull(recovery);
        Assert.Equal(1, recovery.GetTagItem(TelemetryTags.HierarchyVersion));
        Assert.Equal(0, recovery.GetTagItem(TelemetryTags.HierarchyDepth));
        Assert.Equal("", recovery.GetTagItem(TelemetryTags.HierarchyParentSpanId));
        Assert.Equal(recovery.SpanId.ToHexString(), recovery.GetTagItem(TelemetryTags.HierarchySpanId));
        Assert.Equal("Agent", recovery.GetTagItem(TelemetryTags.HierarchyPath));
        Assert.Equal("CLI.Activity.AiRecovery", recovery.OperationName);

        recovery.DisplayName = "a private prompt must not enter the hierarchy";
        using var child = fixture.Service.StartActivity("MsalAuthService.AcquireTokenSilentlyAsync");
        Assert.NotNull(child);
        Assert.False(recovery.IsStopped);
        Assert.Equal(recovery.TraceId, child.TraceId);
        Assert.Equal(recovery.SpanId, child.ParentSpanId);
        Assert.Equal(recovery.SpanId.ToHexString(), child.GetTagItem(TelemetryTags.HierarchyParentSpanId));
        Assert.Equal(1, child.GetTagItem(TelemetryTags.HierarchyDepth));
        Assert.Equal("Agent.MsalAuthService.AcquireTokenSilentlyAsync",
            child.GetTagItem(TelemetryTags.HierarchyPath));
        Assert.NotNull(TelemetryHierarchy.CreateRecoveryHandoff(recovery));
    }

    [Fact]
    public void UnknownAmbientAncestor_IsNotRetrofitted()
    {
        using var fixture = new Fixture();
        using var unknown = fixture.Source.StartActivity("unknown");
        using var child = fixture.Service.StartActivity("Agent.Send");
        Assert.NotNull(unknown);
        Assert.NotNull(child);
        Assert.Equal(unknown.SpanId, child.ParentSpanId);
        Assert.Null(TelemetryHierarchy.Capture(unknown));
        Assert.Null(TelemetryHierarchy.Capture(child));
        Assert.DoesNotContain(child.TagObjects, tag => tag.Key.StartsWith("properties.hierarchy.", StringComparison.Ordinal));
    }

    [Fact]
    public void ExplicitRoot_DoesNotInheritAnUnrelatedAmbientActivity()
    {
        using var fixture = new Fixture();
        using var unrelated = fixture.Service.StartActivity("unrelated");
        using (var root = TelemetryHierarchy.StartActivity(
                   fixture.Source, "Agent.Session", parentContext: default(ActivityContext)))
        {
            Assert.NotNull(root);
            Assert.NotEqual(unrelated!.TraceId, root.TraceId);
            Assert.Equal(default, root.ParentSpanId);
            Assert.Equal(0, root.GetTagItem(TelemetryTags.HierarchyDepth));
            Assert.Equal("Agent.Session", root.GetTagItem(TelemetryTags.HierarchyPath));
        }
        Assert.Same(unrelated, Activity.Current);
    }

    [Fact]
    public void DisabledTelemetry_DoesNotProduceMetadataOrHandoff()
    {
        using var fixture = new Fixture(enabled: false);
        using var recovery = fixture.Service.StartRootActivity(name: "CLI.Activity.AiRecovery");
        Assert.Null(recovery);
        Assert.Null(TelemetryHierarchy.CreateRecoveryHandoff(recovery));
        Assert.Null(fixture.Service.StartActivity("Agent.Send"));
    }

    [Theory]
    [InlineData("CLI.Activity.Root")]
    [InlineData("Agent")]
    public void RecoveryHandoff_RejectsAnUnrelatedRecordedRoot(string operationName)
    {
        using var fixture = new Fixture();
        using var root = fixture.Service.StartRootActivity(name: operationName);
        Assert.Throws<ArgumentException>(() => TelemetryHierarchy.CreateRecoveryHandoff(root));
    }

    [Theory]
    [InlineData("Agent.Session", "Agent.Session")]
    [InlineData("Agent.Create", "Agent.Create")]
    [InlineData("Agent.Turn", "Agent.Turn")]
    [InlineData("Agent.Send", "Agent.Send")]
    [InlineData("Agent.Reprompt", "Agent.Reprompt")]
    [InlineData("tool.get_command", "Agent.Tool.get_command")]
    [InlineData("CLI.Activity.Root", "CLI.Activity.Root")]
    public void StandaloneRoots_KeepTheirNamespaceWithoutInventingParents(string operationName, string expectedPath)
    {
        using var fixture = new Fixture();
        using var root = fixture.Service.StartRootActivity(name: operationName);
        Assert.NotNull(root);
        Assert.Equal(operationName, root.OperationName);
        Assert.Equal(expectedPath, root.GetTagItem(TelemetryTags.HierarchyPath));
        Assert.Equal(0, root.GetTagItem(TelemetryTags.HierarchyDepth));
        Assert.Equal("", root.GetTagItem(TelemetryTags.HierarchyParentSpanId));
        Assert.Equal(default, root.ParentSpanId);
    }

    [Fact]
    public void CanonicalPaths_FollowActualEdgesRatherThanCountingDots()
    {
        using var fixture = new Fixture();
        using var recovery = fixture.Service.StartRootActivity(name: "CLI.Activity.AiRecovery");
        using var session = fixture.Service.StartActivity("Agent.Session");
        using (var create = fixture.Service.StartActivity("Agent.Create"))
        using (var lookup = fixture.Service.StartActivity("tool.get_command"))
        {
            Assert.Equal("Agent.Session.Create", create!.GetTagItem(TelemetryTags.HierarchyPath));
            Assert.Equal("Agent.Session.Create.Tool.get_command", lookup!.GetTagItem(TelemetryTags.HierarchyPath));
            Assert.Equal(3, lookup.GetTagItem(TelemetryTags.HierarchyDepth));
            Assert.Equal(create.SpanId.ToHexString(), lookup.GetTagItem(TelemetryTags.HierarchyParentSpanId));
        }

        var sends = new List<Activity>();
        var tools = new List<Activity>();
        for (var round = 0; round < 2; round++)
        {
            using var turn = fixture.Service.StartActivity("Agent.Turn");
            using var send = fixture.Service.StartActivity("Agent.Send");
            using var tool = fixture.Service.StartActivity("tool.get_command");
            using var nested = fixture.Service.StartActivity("MsalAuthService.AcquireTokenSilentlyAsync");
            Assert.Equal("Agent.Session.Turn", turn!.GetTagItem(TelemetryTags.HierarchyPath));
            Assert.Equal("Agent.Session.Turn.Send", send!.GetTagItem(TelemetryTags.HierarchyPath));
            Assert.Equal("Agent.Session.Turn.Send.Tool.get_command", tool!.GetTagItem(TelemetryTags.HierarchyPath));
            Assert.Equal(4, tool.GetTagItem(TelemetryTags.HierarchyDepth));
            Assert.Equal(send.SpanId, tool.ParentSpanId);
            Assert.Equal("Agent.Session.Turn.Send.Tool.get_command.MsalAuthService.AcquireTokenSilentlyAsync",
                nested!.GetTagItem(TelemetryTags.HierarchyPath));
            Assert.Equal(5, nested.GetTagItem(TelemetryTags.HierarchyDepth));
            Assert.Equal(tool.SpanId, nested.ParentSpanId);
            Assert.Equal("tool.get_command", tool.OperationName);
            Assert.Equal("MsalAuthService.AcquireTokenSilentlyAsync", nested.OperationName);
            sends.Add(send);
            tools.Add(tool);
        }
        Assert.NotEqual(sends[0].SpanId, sends[1].SpanId);
        Assert.NotEqual(tools[0].SpanId, tools[1].SpanId);
        Assert.Equal(tools[0].GetTagItem(TelemetryTags.HierarchyPath), tools[1].GetTagItem(TelemetryTags.HierarchyPath));
        Assert.NotEqual(tools[0].GetTagItem(TelemetryTags.HierarchyParentSpanId),
            tools[1].GetTagItem(TelemetryTags.HierarchyParentSpanId));

        using var retryTurn = fixture.Service.StartActivity("Agent.Turn");
        using var reprompt = fixture.Service.StartActivity("Agent.Reprompt");
        using var retrySend = fixture.Service.StartActivity("Agent.Send");
        Assert.Equal("Agent.Session.Turn.Reprompt", reprompt!.GetTagItem(TelemetryTags.HierarchyPath));
        Assert.Equal(3, reprompt.GetTagItem(TelemetryTags.HierarchyDepth));
        Assert.Equal("Agent.Session.Turn.Reprompt.Send", retrySend!.GetTagItem(TelemetryTags.HierarchyPath));
        Assert.Equal(4, retrySend.GetTagItem(TelemetryTags.HierarchyDepth));
        Assert.Equal(reprompt.SpanId.ToHexString(), retrySend.GetTagItem(TelemetryTags.HierarchyParentSpanId));
    }

    [Fact]
    public void SourceProcessor_RecordsVettedNestedInstrumentationBeforeItReturns()
    {
        using var source = new ActivitySource("hierarchy-processor-" + Guid.NewGuid());
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddProcessor(new SourceHierarchyProcessor(new DefaultTelemetryActivityNameFilter()))
            .Build();

        using var root = source.StartActivity("CLI.Activity.AiRecovery");
        using var nested = source.StartActivity("MsalAuthService.AcquireTokenSilentlyAsync");
        Assert.NotNull(root);
        Assert.NotNull(nested);
        Assert.Equal(0, root.GetTagItem(TelemetryTags.HierarchyDepth));
        Assert.Equal("Agent", root.GetTagItem(TelemetryTags.HierarchyPath));
        Assert.Equal(1, nested.GetTagItem(TelemetryTags.HierarchyDepth));
        Assert.Equal("Agent.MsalAuthService.AcquireTokenSilentlyAsync", nested.GetTagItem(TelemetryTags.HierarchyPath));
        Assert.Equal(root.SpanId.ToHexString(), nested.GetTagItem(TelemetryTags.HierarchyParentSpanId));

        using var unknown = source.StartActivity("private prompt");
        using var knownUnderUnknown = source.StartActivity("Agent.Send");
        Assert.Null(unknown!.GetTagItem(TelemetryTags.HierarchyVersion));
        Assert.Null(knownUnderUnknown!.GetTagItem(TelemetryTags.HierarchyVersion));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ActivityListener _listener;

        internal Fixture(bool enabled = true)
        {
            Source = new ActivitySource("source-hierarchy-" + Guid.NewGuid());
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source == Source,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            };
            ActivitySource.AddActivityListener(_listener);
            Service = new TelemetryService(new Provider(Source), new Configuration(enabled), ClientType.CLI);
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
        public Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public string GetConfigFilePath() => throw new NotSupportedException();
        public IConfigurationSchema GetSchema() => throw new NotSupportedException();
    }
}
