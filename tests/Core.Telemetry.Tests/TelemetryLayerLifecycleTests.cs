// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Trace;
using static Core.Telemetry.Tests.TelemetryLayerTestEnvironment;

namespace Core.Telemetry.Tests;

public class TelemetryLayerLifecycleTests
{
    [Fact]
    public async Task ActualPipeline_ChildFirst_RemainsPartial_UntilIndependentRootCompletes()
    {
        await using var test = new TelemetryLayerTestEnvironment();
        using var ambient = new Activity("unrelated-cli").SetIdFormat(ActivityIdFormat.W3C).Start();
        var scope = test.Create();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        Assert.NotNull(root);
        Assert.Equal(default, root.ParentSpanId);
        Assert.NotEqual(ambient.TraceId, root.TraceId);
        using (var childScope = scope.Telemetry.StartActivityWithParent("Test.Child", root.Context))
        {
            var child = childScope?.Activity;
            Assert.NotNull(child);
            Assert.Null(child.Parent);
            Assert.Equal(root.SpanId, child.ParentSpanId);
            child.SetTag("properties.testlayer.count", 7);
            child.SetTag("unvetted.top.level", "not exported");
            child.SetTag(TelemetryTags.DataBoundary, "Global");
            child.SetTag(TelemetryLayerTags.ServiceName, "CLI");
        }
        await scope.FileSession.WaitForIdleAsync().WaitAsync(TestTimeout);
        Assert.True(File.Exists(scope.FileSession.PartialPath));
        Assert.EndsWith(".partial", scope.FileSession.PartialPath);
        Assert.Empty(new TraceFileStore(test.Root).ClaimPending());
        Assert.Same(ambient, Activity.Current);

        Assert.Equal(TelemetryLayerCompletion.Published, await scope.CompleteAsync(root,
            new Dictionary<string, object?> { ["properties.testlayer.outcome"] = "error" },
            ActivityStatusCode.Error).WaitAsync(TestTimeout));
        rootScope!.Dispose();
        Assert.Same(ambient, Activity.Current);
        Assert.False(File.Exists(scope.FileSession.PartialPath));
        var entries = test.Read(scope);
        Assert.Equal(["Test.Child", "Test.Root"], entries.Select(entry => entry.OperationName));
        Assert.All(entries, entry =>
        {
            Assert.Equal(test.Layer.SourceName, entry.TryGetServiceName());
            Assert.Equal(test.Layer.SourceName, Tag(entry, TelemetryLayerTags.ServiceName).GetString());
            Assert.Equal("test-v1", Tag(entry, TelemetryLayerTags.Schema).GetString());
            Assert.Equal(scope.Context.InvocationId, Tag(entry, TelemetryLayerTags.InvocationId).GetString());
            Assert.Equal("EU", entry.DataBoundary);
            Assert.Equal("EU", Tag(entry, TelemetryTags.DataBoundary).GetString());
            Assert.Equal("resource show", Tag(entry, TelemetryTags.Command).GetString());
            Assert.Equal(scope.Context.CommandTraceId, Tag(entry, TelemetryTags.CommandTraceId).GetString());
        });
        Assert.Equal(7, Tag(entries[0], "properties.testlayer.count").GetInt32());
        Assert.DoesNotContain("unvetted.top.level", entries[0].Tags.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, entries[1].ParentSpanId);
        Assert.Equal("Error", entries[1].Status);
        Assert.Equal("error", Tag(entries[1], "properties.testlayer.outcome").GetString());
        Assert.Single(new TraceFileStore(test.Root).ClaimPending());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GenericParentBasedRootHandle_RestoresCallerAmbientAfterCompletion(bool activate)
    {
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, Diagnostic = _ => { },
            SamplerFactory = () => new ParentBasedSampler(new AlwaysOnSampler()),
        });
        using var ambient = new Activity("unrelated").SetIdFormat(ActivityIdFormat.W3C).Start();
        var scope = test.Create();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: activate);
        Assert.NotNull(rootScope);
        var root = rootScope.Activity;
        Assert.True(root.Recorded);
        Assert.Equal(default, root.ParentSpanId);
        Assert.Null(root.Parent);
        Assert.NotEqual(ambient.TraceId, root.TraceId);
        Assert.Same(activate ? root : ambient, Activity.Current);

        Assert.Equal(TelemetryLayerCompletion.Published, await scope.CompleteAsync(root).WaitAsync(TestTimeout));
        Assert.True(root.IsStopped);
        rootScope.Dispose();
        Assert.Same(ambient, Activity.Current);
        rootScope.Dispose();
        Assert.Same(ambient, Activity.Current);
        Assert.Equal(string.Empty, Assert.Single(test.Read(scope)).ParentSpanId);
    }

    [Fact]
    public async Task GenericParentBasedActiveRootHandle_RestoresAmbientWhenItStopsTheActivity()
    {
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, Diagnostic = _ => { },
            SamplerFactory = () => new ParentBasedSampler(new AlwaysOnSampler()),
        });
        using var ambient = new Activity("unrelated").SetIdFormat(ActivityIdFormat.W3C).Start();
        var scope = test.Create();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root");
        Assert.NotNull(rootScope);
        Assert.Same(rootScope.Activity, Activity.Current);
        Assert.Equal(default, rootScope.Activity.ParentSpanId);
        Assert.Null(rootScope.Activity.Parent);
        rootScope.Dispose();
        Assert.True(rootScope.Activity.IsStopped);
        Assert.Same(ambient, Activity.Current);
        Assert.Equal(TelemetryLayerCompletion.Published, await scope.CompleteAsync().WaitAsync(TestTimeout));
        Assert.Equal(string.Empty, Assert.Single(test.Read(scope)).ParentSpanId);
    }

    [Fact]
    public async Task GenericParentBasedDetachedRootHandle_PreservesTheCurrentCallerAmbient()
    {
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, Diagnostic = _ => { },
            SamplerFactory = () => new ParentBasedSampler(new AlwaysOnSampler()),
        });
        using var original = new Activity("original-caller").SetIdFormat(ActivityIdFormat.W3C).Start();
        var scope = test.Create();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        Assert.NotNull(rootScope);
        Assert.Same(original, Activity.Current);
        using var later = new Activity("later-caller").SetIdFormat(ActivityIdFormat.W3C).Start();
        Assert.Equal(TelemetryLayerCompletion.Published,
            await scope.CompleteAsync(rootScope.Activity).WaitAsync(TestTimeout));
        rootScope.Dispose();
        Assert.Same(later, Activity.Current);
        Assert.False(later.IsStopped);
        Assert.False(original.IsStopped);
        Assert.Equal(string.Empty, Assert.Single(test.Read(scope)).ParentSpanId);
    }

    [Fact]
    public async Task NameGate_RunsBeforeSourceCreation_AndDoesNotCreateRejectedParents()
    {
        await using var test = new TelemetryLayerTestEnvironment();
        var scope = test.Create();
        var started = 0;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source == scope.TelemetryProvider.ActivitySource,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = _ => Interlocked.Increment(ref started),
        };
        ActivitySource.AddActivityListener(listener);
        Assert.Null(scope.Telemetry.StartActivity("http.client.request"));
        Assert.Null(scope.Telemetry.StartActivity("Test.NotAllowed"));
        Assert.Equal(0, started);
        using (var allowedScope = scope.Telemetry.StartIndependentRootActivity("Test.Other"))
            Assert.NotNull(allowedScope?.Activity);
        Assert.Equal(1, started);
        Assert.Equal(TelemetryLayerCompletion.Published, await scope.CompleteAsync().WaitAsync(TestTimeout));
        Assert.Equal("Test.Other", Assert.Single(test.Read(scope)).OperationName);
    }

    [Theory]
    [InlineData("drop")]
    [InlineData("record-only")]
    [InlineData("suppressed")]
    [InlineData("null")]
    public async Task UnsampledSuppressedOrNull_IsNeverSynthesized(string sampling)
    {
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock,
            Diagnostic = _ => { },
            SamplerFactory = sampling switch
            {
                "drop" or "null" => () => new AlwaysOffSampler(),
                "record-only" => () => new RecordOnlySampler(),
                _ => () => new AlwaysOnSampler(),
            },
        });
        var scope = test.Create();
        using var otherListener = new ActivityListener
        {
            ShouldListenTo = source => sampling != "null" && source == scope.TelemetryProvider.ActivitySource,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(otherListener);
        using var suppression = sampling is "suppressed" or "null" ? SuppressInstrumentationScope.Begin() : default;
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        if (sampling == "null")
            Assert.Null(rootScope);
        else
        {
            Assert.NotNull(rootScope);
            Assert.NotNull(root);
        }
        if (sampling == "record-only")
        {
            Assert.True(root!.IsAllDataRequested);
            Assert.False(root.Recorded);
        }

        Assert.Equal(TelemetryLayerCompletion.Empty, await scope.CompleteAsync(root).WaitAsync(TestTimeout));
        Assert.False(Directory.Exists(test.Root));
    }

    [Fact]
    public async Task ProcessorDrop_DoesNotCreateADeadlineReplacement()
    {
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock,
            Diagnostic = _ => { },
            ProcessorFactories = [() => new DropProcessor()],
        });
        var scope = test.Create();
        using (var childScope = scope.Telemetry.StartIndependentRootActivity("Test.Child"))
            Assert.NotNull(childScope?.Activity);
        Assert.Equal(TelemetryLayerCompletion.Empty, await scope.CompleteAsync().WaitAsync(TestTimeout));
        Assert.False(Directory.Exists(test.Root));
    }

    [Fact]
    public async Task ReentrantListenerCannotReplaceTheReservedActivitySamplingDecision()
    {
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock, Diagnostic = _ => { },
            SamplerFactory = () => new DropRootSampler(),
        });
        var scope = test.Create();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source == scope.TelemetryProvider.ActivitySource,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity =>
            {
                if (activity.OperationName == "Test.Root")
                {
                    using var unregistered = scope.TelemetryProvider.ActivitySource.StartActivity("Test.Child");
                    Assert.NotNull(unregistered);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        try
        {
            Assert.NotNull(root);
            Assert.True(root.Recorded);
            Assert.Equal(TelemetryLayerCompletion.Empty, await scope.CompleteAsync(root).WaitAsync(TestTimeout));
            Assert.False(Directory.Exists(test.Root));
        }
        finally
        {
            await test.CompleteForCleanupAsync(scope, root);
            rootScope?.Dispose();
        }
    }

    [Fact]
    public async Task ProcessorDrop_AppliesToTheRootCompletedByTheScopeToo()
    {
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock,
            Diagnostic = _ => { },
            ProcessorFactories = [() => new DropProcessor()],
        });
        var scope = test.Create();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        Assert.NotNull(root);
        Assert.Equal(TelemetryLayerCompletion.Empty, await scope.CompleteAsync(root).WaitAsync(TestTimeout));
        Assert.False(Directory.Exists(test.Root));
    }

    [Fact]
    public async Task CallbackWithoutActivity_IsTrackedUntilItsActualBodyFinishes()
    {
        await using var test = new TelemetryLayerTestEnvironment();
        var scope = test.Create();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        var callback = scope.TryEnterProducer();
        Assert.NotNull(callback);
        var completion = scope.CompleteAsync(root);
        await scope.WaitForStateAsync(TelemetryLayerState.Draining).WaitAsync(TestTimeout);
        Assert.Null(scope.TryEnterProducer());
        Assert.False(completion.IsCompleted);
        Assert.False(Directory.Exists(test.Root));
        callback.Dispose();
        Assert.Equal(TelemetryLayerCompletion.Published, await completion.WaitAsync(TestTimeout));
        Assert.Single(test.Read(scope));
    }

    [Fact]
    public async Task Deadline_UsesImmutableStartMetadataAndFrozenResult_ThenRejectsLateEnd()
    {
        await using var test = new TelemetryLayerTestEnvironment();
        var scope = test.Create();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        using var producer = scope.TryEnterProducer();
        using var childScope = scope.Telemetry.StartActivityWithParent("Test.Child", root!.Context);
        var child = childScope?.Activity;
        Assert.NotNull(child);
        child.SetTag("properties.testlayer.live", new ThrowIfSerialized());
        var terminal = new Dictionary<string, object?> { ["properties.testlayer.outcome"] = "cancelled" };
        var completion = scope.CompleteAsync(root, terminal, ActivityStatusCode.Error);
        terminal["properties.testlayer.outcome"] = "changed after freeze";
        test.Clock.Advance(LayerTelemetryScope.ProducerDrainBudget);
        await scope.WaitForStateAsync(TelemetryLayerState.Fenced).WaitAsync(TestTimeout);
        Assert.Equal(TelemetryLayerCompletion.PublishedAtDeadline, await completion.WaitAsync(TestTimeout));

        var entries = test.Read(scope);
        Assert.Equal(2, entries.Count);
        var abandoned = entries.Single(entry => entry.OperationName == "Test.Child");
        Assert.Equal("abandoned", Tag(abandoned, TelemetryLayerTags.Completion).GetString());
        Assert.DoesNotContain("properties.testlayer.live", abandoned.Tags.GetRawText(), StringComparison.Ordinal);
        var terminalRoot = entries.Single(entry => entry.OperationName == "Test.Root");
        Assert.Equal("Error", terminalRoot.Status);
        Assert.Equal("cancelled", Tag(terminalRoot, "properties.testlayer.outcome").GetString());
        Assert.Equal("deadline", Tag(terminalRoot, TelemetryLayerTags.Completion).GetString());
        Assert.Equal(1, Tag(terminalRoot, TelemetryLayerTags.AbandonedSpans).GetInt32());
        Assert.True(Tag(terminalRoot, TelemetryLayerTags.PendingProducers).GetInt32() >= 1);
        Assert.False(child.IsStopped);
        var bytes = File.ReadAllBytes(scope.FileSession.PublishedPath!);
        childScope!.Dispose();
        Assert.Null(scope.Telemetry.StartActivity("Test.Other"));
        Assert.Null(scope.TryEnterProducer(teardown: true));
        Assert.Equal(bytes, File.ReadAllBytes(scope.FileSession.PublishedPath!));
        Assert.False(File.Exists(scope.FileSession.PartialPath));
    }

    [Fact]
    public async Task CaptureVersusDeadline_HasOneTerminalOwner()
    {
        using var capture = new TelemetryLayerBarrier();
        var armed = false;
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock,
            Diagnostic = _ => { },
            Checkpoint = point =>
            {
                if (armed && point == TelemetryLayerCheckpoint.CaptureReserved)
                    capture.Hit();
            },
        });
        var scope = test.Create();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        using var childScope = scope.Telemetry.StartActivityWithParent("Test.Child", root!.Context)!;
        var child = childScope.Activity;
        armed = true;
        var end = Task.Run(child.Stop);
        await capture.Entered.Task.WaitAsync(TestTimeout);
        var completion = scope.CompleteAsync(root);
        test.Clock.Advance(LayerTelemetryScope.ProducerDrainBudget);
        await scope.WaitForStateAsync(TelemetryLayerState.Fenced).WaitAsync(TestTimeout);
        Assert.False(completion.IsCompleted);
        capture.Release();
        await end.WaitAsync(TestTimeout);
        childScope.Dispose();
        Assert.Equal(TelemetryLayerCompletion.PublishedAtDeadline, await completion.WaitAsync(TestTimeout));
        var entries = test.Read(scope);
        Assert.Equal(2, entries.Count);
        Assert.Single(entries, entry => entry.SpanId == child.SpanId.ToString());
        Assert.Equal("abandoned", Tag(entries.Single(entry => entry.OperationName == "Test.Child"),
            TelemetryLayerTags.Completion).GetString());
    }

    [Fact]
    public async Task StoppedBeforeExporterEntry_IsCoveredWithoutRelyingOnListenerOrder()
    {
        using var endBarrier = new TelemetryLayerBarrier();
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock,
            Diagnostic = _ => { },
            ProcessorFactories = [() => new BlockingEndProcessor(endBarrier)],
        });
        var scope = test.Create();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        using var childScope = scope.Telemetry.StartActivityWithParent("Test.Child", root!.Context)!;
        var child = childScope.Activity;
        var end = Task.Run(child.Stop);
        await endBarrier.Entered.Task.WaitAsync(TestTimeout);
        var completion = scope.CompleteAsync(root);
        test.Clock.Advance(LayerTelemetryScope.ProducerDrainBudget);
        Assert.Equal(TelemetryLayerCompletion.PublishedAtDeadline, await completion.WaitAsync(TestTimeout));
        var bytes = File.ReadAllBytes(scope.FileSession.PublishedPath!);
        endBarrier.Release();
        await end.WaitAsync(TestTimeout);
        childScope.Dispose();
        Assert.Equal(bytes, File.ReadAllBytes(scope.FileSession.PublishedPath!));
        Assert.Equal(2, test.Read(scope).Count);
    }

    [Fact]
    public async Task CreationReservation_MustHandOffBeforePublication_AndLateSamplerCannotReviveIt()
    {
        using var creation = new TelemetryLayerBarrier();
        var armed = false;
        await using var test = new TelemetryLayerTestEnvironment(clock => new TelemetryLayerScopeOptions
        {
            TimeProvider = clock,
            Diagnostic = _ => { },
            Checkpoint = point =>
            {
                if (armed && point == TelemetryLayerCheckpoint.CreationReserved)
                    creation.Hit();
            },
        });
        var scope = test.Create();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        armed = true;
        var start = Task.Run(() => scope.Telemetry.StartActivity("Test.Child"));
        await creation.Entered.Task.WaitAsync(TestTimeout);
        var completion = scope.CompleteAsync(root);
        test.Clock.Advance(LayerTelemetryScope.ProducerDrainBudget);
        await scope.WaitForStateAsync(TelemetryLayerState.Fenced).WaitAsync(TestTimeout);
        Assert.False(completion.IsCompleted);
        Assert.False(Directory.Exists(test.Root));
        creation.Release();
        using var late = await start.WaitAsync(TestTimeout);
        Assert.True(late is null || (!late.Recorded && !late.IsAllDataRequested));
        Assert.Equal(TelemetryLayerCompletion.PublishedAtDeadline, await completion.WaitAsync(TestTimeout));
        Assert.Single(test.Read(scope));
    }

    [Fact]
    public async Task RecordedCreationAlreadyInsideListener_HandsOffItsImmutableIdentityAtDeadline()
    {
        using var creation = new TelemetryLayerBarrier();
        await using var test = new TelemetryLayerTestEnvironment();
        var scope = test.Create();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source == scope.TelemetryProvider.ActivitySource,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity =>
            {
                if (activity.OperationName == "Test.Child")
                    creation.Hit();
            },
        };
        ActivitySource.AddActivityListener(listener);
        var start = Task.Run(() => scope.Telemetry.StartActivityWithParent("Test.Child", root!.Context));
        await creation.Entered.Task.WaitAsync(TestTimeout);
        var completion = scope.CompleteAsync(root);
        test.Clock.Advance(LayerTelemetryScope.ProducerDrainBudget);
        await scope.WaitForStateAsync(TelemetryLayerState.Fenced).WaitAsync(TestTimeout);
        Assert.False(completion.IsCompleted);
        Assert.False(Directory.Exists(test.Root));
        creation.Release();
        using var childScope = await start.WaitAsync(TestTimeout);
        var child = childScope?.Activity;
        Assert.NotNull(child);
        Assert.True(child.Recorded);
        Assert.Equal(TelemetryLayerCompletion.PublishedAtDeadline, await completion.WaitAsync(TestTimeout));
        var entries = test.Read(scope);
        Assert.Equal(2, entries.Count);
        var abandoned = entries.Single(entry => entry.OperationName == "Test.Child");
        Assert.Equal(child.SpanId.ToString(), abandoned.SpanId);
        Assert.Equal(root!.SpanId.ToString(), abandoned.ParentSpanId);
        Assert.Equal("abandoned", Tag(abandoned, TelemetryLayerTags.Completion).GetString());
    }

    [Fact]
    public async Task DisabledScope_StillBudgetsSynchronousCleanup_WithoutFilesOrProvider()
    {
        using var cleanup = new TelemetryLayerBarrier();
        await using var test = new TelemetryLayerTestEnvironment();
        var scope = test.Create(test.Context(enabled: false));
        Assert.Same(TracerProvider.Default, scope.TelemetryProvider.TracerProvider);
        Assert.Null(scope.Telemetry.StartIndependentRootActivity("Test.Root"));
        var cleanupTask = scope.RunCleanupAsync(() =>
        {
            cleanup.Hit();
            return Task.CompletedTask;
        });
        await cleanup.Entered.Task.WaitAsync(TestTimeout);
        test.Clock.Advance(LayerTelemetryScope.ProducerDrainBudget);
        Assert.False(await cleanupTask.WaitAsync(TestTimeout));
        var completion = scope.CompleteAsync();
        Assert.Equal(TelemetryLayerCompletion.Disabled, await completion.WaitAsync(TestTimeout));
        Assert.False(Directory.Exists(test.Root));
        Assert.True(scope.Context.Enabled is false);
        cleanup.Release();
    }

    [Fact]
    public async Task CancellationCallback_IsDispatchedAndIncludedInTheSameDrainBudget()
    {
        using var cancellation = new TelemetryLayerBarrier();
        await using var test = new TelemetryLayerTestEnvironment();
        var scope = test.Create();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        using var registration = scope.StoppingToken.Register(cancellation.Hit);
        scope.BeginDrain();
        await cancellation.Entered.Task.WaitAsync(TestTimeout);
        test.Clock.Advance(TimeSpan.FromSeconds(9));
        Assert.Equal(TimeSpan.FromSeconds(1), scope.RemainingDrainTime);
        var completion = scope.CompleteAsync(root);
        test.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(TelemetryLayerCompletion.PublishedAtDeadline, await completion.WaitAsync(TestTimeout));
        Assert.Equal(1, Tag(Assert.Single(test.Read(scope)), TelemetryLayerTags.PendingProducers).GetInt32());
        cancellation.Release();
    }

    [Fact]
    public async Task CleanupFailure_PreservesBusinessResult_AndRegisteredTeardownCanRecord()
    {
        await using var test = new TelemetryLayerTestEnvironment();
        var scope = test.Create();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        var order = new List<string>();
        scope.RegisterCleanup(() =>
        {
            order.Add("context");
            return Task.CompletedTask;
        }, order: 20);
        scope.RegisterCleanup(() =>
        {
            order.Add("sdk");
            using var callback = scope.TryEnterProducer(teardown: true);
            Assert.NotNull(callback);
            using var childScope = scope.Telemetry.StartActivityWithParent("Test.Child", root!.Context);
            Assert.NotNull(childScope?.Activity);
            throw new InvalidOperationException("Sensitive business details must not appear in diagnostics");
        }, order: 10);
        Assert.Equal(TelemetryLayerCompletion.Published, await scope.CompleteAsync(root,
            new Dictionary<string, object?> { ["properties.testlayer.outcome"] = "error" },
            ActivityStatusCode.Error).WaitAsync(TestTimeout));
        Assert.Equal(["sdk", "context"], order);
        Assert.Contains("cleanup_failed", scope.Diagnostics);
        Assert.DoesNotContain(scope.Diagnostics, code => code.Contains("Sensitive", StringComparison.Ordinal));
        var entry = test.Read(scope).Single(item => item.OperationName == "Test.Root");
        Assert.Equal("Error", entry.Status);
        Assert.Equal("error", Tag(entry, "properties.testlayer.outcome").GetString());
        Assert.False(scope.FileSession.Failed);
    }

    [Fact]
    public async Task CleanupCannotRewriteTheFrozenRootStatus_WhenNoOverrideWasSupplied()
    {
        await using var test = new TelemetryLayerTestEnvironment();
        var scope = test.Create();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity("Test.Root", activate: false);
        var root = rootScope?.Activity;
        root!.SetStatus(ActivityStatusCode.Error);
        scope.RegisterCleanup(() =>
        {
            root.SetStatus(ActivityStatusCode.Ok);
            root.SetTag("properties.testlayer.outcome", "wrong");
            return Task.CompletedTask;
        });
        Assert.Equal(TelemetryLayerCompletion.Published, await scope.CompleteAsync(root,
            new Dictionary<string, object?> { ["properties.testlayer.outcome"] = "error" }).WaitAsync(TestTimeout));
        var entry = Assert.Single(test.Read(scope));
        Assert.Equal("Error", entry.Status);
        Assert.Equal("error", Tag(entry, "properties.testlayer.outcome").GetString());
    }

    private sealed class RecordOnlySampler : Sampler
    {
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters) =>
            new(SamplingDecision.RecordOnly);
    }

    private sealed class DropRootSampler : Sampler
    {
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters) =>
            new(samplingParameters.Name == "Test.Root" ? SamplingDecision.Drop : SamplingDecision.RecordAndSample);
    }

    private sealed class DropProcessor : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data) => data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
    }

    private sealed class BlockingEndProcessor(TelemetryLayerBarrier barrier) : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data)
        {
            if (data.OperationName == "Test.Child")
                barrier.Hit();
        }
    }

    private sealed class ThrowIfSerialized
    {
        public string Value => throw new InvalidOperationException("Live activity tags were enumerated");
    }
}
