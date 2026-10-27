// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry;
using static Core.Telemetry.Tests.TelemetryLayerTestEnvironment;

namespace Core.Telemetry.Tests;

public class TelemetryLayerFactoryTests
{
    [Fact]
    public async Task KnownAgentSource_RejectsOverlapWithoutChangingInvocationPermission()
    {
        await using var test = new TelemetryLayerTestEnvironment(layer: TelemetryLayers.Agent);
        var scope = test.Create();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Agent.Session", activate: false);
        var root = rootScope?.Activity;
        Assert.NotNull(root);
        var blocked = test.Create(factory: test.NewFactory(TelemetryLayers.Agent));
        Assert.True(blocked.Context.Enabled);
        Assert.Null(blocked.Telemetry.StartIndependentRootActivity("Agent.Session"));
        Assert.Contains("source_already_owned", blocked.Diagnostics);
        Assert.Equal(TelemetryLayerCompletion.NotPublished, await blocked.CompleteAsync().WaitAsync(TestTimeout));
        Assert.Equal(TelemetryLayerCompletion.Published, await scope.CompleteAsync(root).WaitAsync(TestTimeout));
        var next = test.Create(factory: test.NewFactory(TelemetryLayers.Agent));
        using (var childScope = next.Telemetry.StartIndependentRootActivity("tool.help"))
            Assert.NotNull(childScope?.Activity);
        Assert.Equal(TelemetryLayerCompletion.Published, await next.CompleteAsync().WaitAsync(TestTimeout));
        Assert.All(test.Read(scope), entry =>
        {
            Assert.Equal("Agent", entry.TryGetServiceName());
            Assert.Equal("agent-v1", Tag(entry, TelemetryLayerTags.Schema).GetString());
        });
    }

    [Fact]
    public async Task FreshProcessors_DisposeExactlyOnce_AndBorrowedServiceDoesNotDisposeProvider()
    {
        var instances = new List<CountingProcessor>();
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, Diagnostic = _ => { },
            ProcessorFactories =
            [
                () =>
                {
                    var processor = new CountingProcessor();
                    instances.Add(processor);
                    return processor;
                },
            ],
        });
        var first = test.Create();
        using var rootScope = first.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        first.Telemetry.Dispose();
        first.Telemetry.Dispose();
        Assert.Equal(0, Assert.Single(instances).Disposals);
        Assert.Equal(TelemetryLayerCompletion.Published, await first.CompleteAsync(root).WaitAsync(TestTimeout));
        await first.DisposeAsync();
        Assert.Equal(1, instances[0].Disposals);
        var second = test.Create();
        using (second.Telemetry.StartIndependentRootActivity("Test.Child")) { }
        Assert.Equal(TelemetryLayerCompletion.Published, await second.CompleteAsync().WaitAsync(TestTimeout));
        Assert.Equal(2, instances.Count);
        Assert.NotSame(instances[0], instances[1]);
        Assert.All(instances, processor => Assert.Equal(1, processor.Disposals));
    }

    [Fact]
    public async Task TimedOutProviderDisposal_KeepsSourceExclusiveUntilActualRelease()
    {
        using var disposal = new TelemetryLayerBarrier();
        var count = 0;
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, Diagnostic = _ => { },
            ProcessorFactories = [() => new CountingProcessor(
                Interlocked.Increment(ref count) == 1 ? disposal.Hit : null)],
        });
        var first = test.Create();
        using (first.Telemetry.StartIndependentRootActivity("Test.Child")) { }
        var completion = first.CompleteAsync();
        await disposal.Entered.Task.WaitAsync(TestTimeout);
        test.Clock.Advance(LayerTelemetryScope.FileCompletionBudget);
        Assert.Equal(TelemetryLayerCompletion.NotPublished, await completion.WaitAsync(TestTimeout));
        var blocked = test.Create();
        Assert.Null(blocked.Telemetry.StartActivity("Test.Child"));
        Assert.True(blocked.Context.Enabled);
        Assert.Equal(TelemetryLayerCompletion.NotPublished, await blocked.CompleteAsync().WaitAsync(TestTimeout));
        Assert.Equal(1, count);
        disposal.Release();
        Assert.True(await first.ProviderDisposalCompletion.WaitAsync(TestTimeout));
        Assert.False(File.Exists(first.FileSession.PublishedPath));
        var next = test.Create();
        using (var childScope = next.Telemetry.StartIndependentRootActivity("Test.Child"))
            Assert.NotNull(childScope?.Activity);
        Assert.Equal(TelemetryLayerCompletion.Published, await next.CompleteAsync().WaitAsync(TestTimeout));
        Assert.Equal(2, count);
        Assert.True(File.Exists(first.FileSession.PartialPath));
    }

    [Fact]
    public async Task FailedProviderConstruction_IsNotOptOut_AndDoesNotGuessThatReleaseSucceeded()
    {
        var attempts = 0;
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, Diagnostic = _ => { },
            SamplerFactory = () =>
            {
                Interlocked.Increment(ref attempts);
                throw new InvalidOperationException("Injected construction failure");
            },
        });
        var failed = test.Create();
        Assert.True(failed.Context.Enabled);
        Assert.Null(failed.Telemetry.StartActivity("Test.Child"));
        Assert.Equal(TelemetryLayerCompletion.NotPublished, await failed.CompleteAsync().WaitAsync(TestTimeout));
        var blocked = test.Create();
        Assert.True(blocked.Context.Enabled);
        Assert.Contains("source_already_owned", blocked.Diagnostics);
        Assert.Equal(TelemetryLayerCompletion.NotPublished, await blocked.CompleteAsync().WaitAsync(TestTimeout));
        Assert.Equal(1, attempts);
        Assert.False(Directory.Exists(test.Root));
    }

    [Theory]
    [InlineData("EU", "EU")]
    [InlineData("eu", "EU")]
    [InlineData("Global", "Global")]
    [InlineData("unknown", "unknown")]
    [InlineData("APAC", "APAC")]
    [InlineData(null, "Unknown")]
    public async Task InvocationBoundary_IsUniformOnEveryRecord(string? boundary, string effective)
    {
        await using var test = new TelemetryLayerTestEnvironment();
        var scope = test.Create(test.Context() with { DataBoundary = boundary });
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        using (var childScope = scope.Telemetry.StartActivityWithParent("ProfileService.Load", root!.Context))
        {
            var child = childScope?.Activity;
            Assert.NotNull(child);
            Assert.Equal(effective, child.GetTagItem(TelemetryTags.DataBoundary));
        }
        Assert.Equal(TelemetryLayerCompletion.Published, await scope.CompleteAsync(root).WaitAsync(TestTimeout));
        Assert.All(test.Read(scope), entry =>
        {
            Assert.Equal(effective, entry.DataBoundary);
            Assert.Equal(effective, Tag(entry, TelemetryTags.DataBoundary).GetString());
        });
    }

    [Fact]
    public async Task GenericDescriptor_UsesActualPipelineAndUploader_WithoutAgentConstants()
    {
        var layer = new TelemetryLayer(
            "TestLayer" + Guid.NewGuid().ToString("N"), "testlayer", "testlayer-v9",
            ["Test.Root", "Test.Child"],
            propertyNames: [TelemetryTags.CommandName],
            propertyPrefixes: ["properties.testlayer."]);
        await using var test = new TelemetryLayerTestEnvironment(layer: layer);
        var scope = test.Create();
        var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        try
        {
            using (var childScope = scope.Telemetry.StartActivityWithParent("Test.Child", root!.Context))
            {
                var child = childScope?.Activity;
                Assert.NotNull(child);
                child.SetTag("properties.testlayer.count", 23);
                child.SetTag("properties.otherLayer.count", 99);
                child.SetTag("http.host", "not admitted by narrower policy");
            }
            var sink = new CapturingSink();
            var uploader = new TraceUploader(new TraceFileStore(test.Root), sink, [layer]);
            uploader.Run();
            Assert.Empty(sink.Entries);
            Assert.Equal(TelemetryLayerCompletion.Published, await scope.CompleteAsync(root).WaitAsync(TestTimeout));
            uploader.Run();
            Assert.Equal(layer.ServiceName, sink.Service);
            Assert.Equal("EU", sink.Boundary);
            Assert.Equal(TraceReplayProfile.ManagedLayer, sink.Profile);
            Assert.Equal(2, sink.Entries.Count);
            var entry = sink.Entries.Single(item => item.OperationName == "Test.Child");
            Assert.Equal(23, Tag(entry, "properties.testlayer.count").GetInt32());
            Assert.DoesNotContain("properties.otherLayer.count", entry.Tags.GetRawText(), StringComparison.Ordinal);
            Assert.DoesNotContain("http.host", entry.Tags.GetRawText(), StringComparison.Ordinal);
            Assert.Equal("testlayer-v9", Tag(entry, TelemetryLayerTags.Schema).GetString());
            Assert.Empty(Directory.GetFiles(test.Root, "traces-*"));
        }
        finally
        {
            await test.CompleteForCleanupAsync(scope, root);
            rootScope?.Dispose();
        }
    }

    [Fact]
    public void Descriptor_CannotWidenTheWireVocabulary_OrUseLegacySourceNames()
    {
        Assert.Throws<ArgumentException>(() => new TelemetryLayer(
            "Test", "test", "test-v1", ["Test.Root"], propertyNames: ["arbitrary.top.level"]));
        Assert.Throws<ArgumentException>(() => new TelemetryLayer(
            "Test", "test", "test-v1", ["Test.Root"], propertyPrefixes: ["arbitrary."]));
        Assert.Throws<ArgumentException>(() => new TelemetryLayer("CLI", "test", "v1", ["Test.Root"]));
        Assert.Throws<ArgumentException>(() => new TelemetryLayer("cli", "test", "v1", ["Test.Root"]));
        Assert.Throws<ArgumentException>(() => new TelemetryLayer("Agent*", "test", "v1", ["Test.Root"]));
        Assert.Throws<ArgumentException>(() => new TelemetryLayer("Test", "..\\test", "v1", ["Test.Root"]));
        Assert.Throws<ArgumentException>(() => new TelemetryLayer("Test", "Uppercase", "v1", ["Test.Root"]));
        Assert.Equal(TelemetryLayers.Agent.ServiceName, TelemetryLayers.Agent.SourceName);
        Assert.True(TelemetryLayers.Agent.NameFilter.IsAllowed("MsalAuthService.GetAccessTokenAsync"));
        Assert.True(TelemetryLayers.Agent.NameFilter.IsAllowed("ProfileService.Load"));
        Assert.False(TelemetryLayers.Agent.NameFilter.IsAllowed("http.request"));
    }

    [Fact]
    public async Task InvalidIds_AreRejectedBeforeSourceOwnershipOrFileCreation()
    {
        await using var test = new TelemetryLayerTestEnvironment();
        Assert.Throws<ArgumentException>(() => test.Factory.CreateScope(
            test.Layer.ServiceName, test.Context() with { InvocationId = "..\\bad" }));
        Assert.Throws<ArgumentException>(() => test.Factory.CreateScope(
            test.Layer.ServiceName, test.Context(), fileId: Guid.Empty.ToString("N")));
        Assert.Throws<ArgumentException>(() => test.Factory.CreateScope(
            test.Layer.ServiceName, test.Context(), fileId: "..\\bad"));
        Assert.Throws<ArgumentException>(() => test.Factory.CreateScope(
            test.Layer.ServiceName, test.Context(), fileId: " " + Guid.NewGuid().ToString("N") + " "));
        Assert.Throws<ArgumentException>(() => test.Factory.CreateScope(
            test.Layer.ServiceName, test.Context() with { InvocationId = " " + Guid.NewGuid().ToString("N") }));
        Assert.False(Directory.Exists(test.Root));
        var scope = test.Create();
        Assert.Equal(TelemetryLayerCompletion.Empty, await scope.CompleteAsync().WaitAsync(TestTimeout));
    }

    [Fact]
    public async Task LiveOptOutGate_StillAppliesWithinAnEnabledLayer()
    {
        await using var test = new TelemetryLayerTestEnvironment();
        var scope = test.Create();
        test.Configuration.Enabled = false;
        Assert.Null(scope.Telemetry.StartIndependentRootActivity("Test.Root"));
        Assert.Null(scope.Telemetry.StartActivityWithParent("Test.Child", default));
        Assert.Equal(TelemetryLayerCompletion.Empty, await scope.CompleteAsync().WaitAsync(TestTimeout));
        Assert.False(Directory.Exists(test.Root));
    }

    [Fact]
    public async Task MissingSnapshotMetadata_IsNotReplacedWithLaterMutableSpanTags()
    {
        await using var test = new TelemetryLayerTestEnvironment();
        var scope = test.Create(new TelemetryInvocationContext(Guid.NewGuid().ToString("N"), true));
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        root!.SetTag(TelemetryTags.CommandName, "not the frozen command");
        root.SetTag(TelemetryTags.TenantId, "not the frozen tenant");
        Assert.Equal(TelemetryLayerCompletion.Published, await scope.CompleteAsync(root).WaitAsync(TestTimeout));
        var entry = Assert.Single(test.Read(scope));
        Assert.Null(entry.TenantId);
        Assert.Equal("Unknown", entry.DataBoundary);
        Assert.DoesNotContain(entry.Tags.EnumerateArray(),
            tag => tag.GetProperty("Key").GetString() is TelemetryTags.CommandName or TelemetryTags.TenantId);
    }

    [Fact]
    public async Task CanonicalMetadataCasingAndRootIdentity_CannotBeOverriddenBySpanTags()
    {
        await using var test = new TelemetryLayerTestEnvironment();
        var scope = test.Create();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source == scope.TelemetryProvider.ActivitySource,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity =>
            {
                activity.SetTag("SCHEMA", "wrong");
                activity.SetTag("SERVICE.NAME", "CLI");
                activity.SetTag("databoundary", "Global");
            },
        };
        ActivitySource.AddActivityListener(listener);
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        root!.SetTag(TelemetryTags.ClientType, "CLI");
        Assert.Equal(TelemetryLayerCompletion.Published, await scope.CompleteAsync(root).WaitAsync(TestTimeout));
        var entry = Assert.Single(test.Read(scope));
        Assert.Equal("test-v1", Tag(entry, TelemetryLayerTags.Schema).GetString());
        Assert.Equal(test.Layer.ServiceName, Tag(entry, TelemetryLayerTags.ServiceName).GetString());
        Assert.Equal(test.Layer.ServiceName, Tag(entry, TelemetryTags.ClientType).GetString());
        Assert.Equal("EU", Tag(entry, TelemetryTags.DataBoundary).GetString());
        var sink = new CapturingSink();
        new TraceUploader(new TraceFileStore(test.Root), sink, [test.Layer]).Run();
        Assert.Single(sink.Entries);
    }

    [Fact]
    public async Task ChildActivity_UsesLayerClientName()
    {
        await using var test = new TelemetryLayerTestEnvironment();
        var scope = test.Create();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root");
        var root = rootScope?.Activity;
        Assert.NotNull(root);

        using (var child = scope.Telemetry.StartActivity("Test.Child"))
        {
            Assert.NotNull(child);
            Assert.Equal(test.Layer.ServiceName, child.GetTagItem(TelemetryTags.ClientType));
        }

        Assert.Equal(TelemetryLayerCompletion.Published, await scope.CompleteAsync(root).WaitAsync(TestTimeout));
    }

    private sealed class CountingProcessor(Action? beforeDispose = null) : BaseProcessor<Activity>
    {
        private int _disposals;
        internal int Disposals => Volatile.Read(ref _disposals);
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                beforeDispose?.Invoke();
                Interlocked.Increment(ref _disposals);
            }
            base.Dispose(disposing);
        }
    }

    private sealed class CapturingSink : ITraceExportSink
    {
        internal string? Service { get; private set; }
        internal string? Boundary { get; private set; }
        internal TraceReplayProfile Profile { get; private set; }
        internal List<TraceEntry> Entries { get; } = [];
        public bool TryExport(
            string serviceName, IReadOnlyList<TraceEntry> entries,
            string? dataBoundary = null, TraceReplayProfile profile = TraceReplayProfile.Legacy,
            TraceReplayContext? replayContext = null)
        {
            Service = serviceName;
            Boundary = dataBoundary;
            Profile = profile;
            Entries.AddRange(entries);
            return true;
        }
    }
}
