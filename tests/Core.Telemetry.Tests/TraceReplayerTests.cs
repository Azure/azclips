// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Tests.Common.Utilities;

namespace Core.Telemetry.Tests;

/// <summary>
/// Tests for <see cref="TraceReplayer"/> (ordering and context remapping).
/// </summary>
public class TraceReplayerTests
{
    private const string SampleTraceId = "0123456789abcdef0123456789abcdef";
    private const string ZeroSpanId = "0000000000000000";

    [Fact]
    public void ReplayEntries_ShouldDoNothing_WhenEntriesIsEmpty()
    {
        using var harness = new ReplayHarness();

        TraceReplayer.ReplayEntries(harness.Source, Array.Empty<TraceEntry>());

        Assert.Empty(harness.Stopped);
    }

    [Fact]
    public void ReplayEntries_ShouldSkipEntry_WhenTraceIdIsEmpty()
    {
        using var harness = new ReplayHarness();
        var entry = MakeEntry(traceId: null, spanId: "1111111111111111", parentSpanId: null, name: "op");

        TraceReplayer.ReplayEntries(harness.Source, new[] { entry });

        Assert.Empty(harness.Stopped);
    }

    [Fact]
    public void ReplayEntries_ShouldEmitActivity_PreservingTraceIdAndName()
    {
        using var harness = new ReplayHarness();
        var entry = MakeEntry(SampleTraceId, "1111111111111111", parentSpanId: null, name: "root-op");

        TraceReplayer.ReplayEntries(harness.Source, new[] { entry });

        var activity = Assert.Single(harness.Stopped);
        Assert.Equal(SampleTraceId, activity.TraceId.ToString());
        Assert.Equal("root-op", activity.OperationName);
    }

    [Theory]
    [InlineData(null, "ROW")]
    [InlineData("Unknown", "ROW")]
    [InlineData("Global", "ROW")]
    [InlineData("APAC", "ROW")]
    [InlineData("EU", "EU")]
    public void ReplayEntries_ExportsNormalizedBoundary_ForLegacyRecords(string? boundary, string expected)
    {
        using var harness = new ReplayHarness();
        var entry = MakeEntry(SampleTraceId, "1111111111111111", parentSpanId: null, name: "root-op");
        entry.DataBoundary = boundary;

        TraceReplayer.ReplayEntries(harness.Source, [entry]);

        Assert.Equal(expected, Assert.Single(harness.Stopped).GetTagItem(TelemetryTags.DataBoundary));
    }

    [Fact]
    public void ReplayEntries_ShouldPreserveParentChildHierarchy_WhenParentPrecedesChild()
    {
        using var harness = new ReplayHarness();
        var parent = MakeEntry(SampleTraceId, "1111111111111111", parentSpanId: null, name: "parent");
        var child = MakeEntry(SampleTraceId, "2222222222222222", parentSpanId: "1111111111111111", name: "child");

        TraceReplayer.ReplayEntries(harness.Source, new[] { parent, child });

        Assert.Equal(2, harness.Stopped.Count);
        var parentActivity = harness.Stopped.Single(a => a.OperationName == "parent");
        var childActivity = harness.Stopped.Single(a => a.OperationName == "child");
        Assert.Equal(parentActivity.SpanId, childActivity.ParentSpanId);
        Assert.Equal(parentActivity.TraceId, childActivity.TraceId);
    }

    [Fact]
    public void ReplayEntries_LinksRecoveryAndSession_WhenMergedIntoOneBatch()
    {
        using var harness = new ReplayHarness();
        // This exercises one replay batch; TracerProviderExportSinkTests covers separate files/providers.
        var recovery = MakeEntry(SampleTraceId, "00f067aa0ba902b7", parentSpanId: ZeroSpanId, name: "CLI.Activity.AiRecovery");
        var session = MakeEntry(SampleTraceId, "a1b2c3d4e5f60718", parentSpanId: "00f067aa0ba902b7", name: "Agent.Session");

        TraceReplayer.ReplayEntries(harness.Source, new[] { recovery, session });

        var replayedRecovery = harness.Stopped.Single(a => a.OperationName == "CLI.Activity.AiRecovery");
        var replayedSession = harness.Stopped.Single(a => a.OperationName == "Agent.Session");

        Assert.Equal(SampleTraceId, replayedSession.TraceId.ToString());
        Assert.Equal(replayedRecovery.SpanId, replayedSession.ParentSpanId);
    }

    [Fact]
    public void ReplayEntries_ShouldOrderEntriesTopologically_WhenChildPrecedesParent()
    {
        using var harness = new ReplayHarness();
        var parent = MakeEntry(SampleTraceId, "1111111111111111", parentSpanId: null, name: "parent");
        var child = MakeEntry(SampleTraceId, "2222222222222222", parentSpanId: "1111111111111111", name: "child");

        TraceReplayer.ReplayEntries(harness.Source, new[] { child, parent });

        Assert.Equal(2, harness.Stopped.Count);
        Assert.Equal("parent", harness.Stopped[0].OperationName);
        Assert.Equal("child", harness.Stopped[1].OperationName);
        Assert.Equal(harness.Stopped[0].SpanId, harness.Stopped[1].ParentSpanId);
    }

    [Fact]
    public void ReplayEntries_ShouldTreatEntryAsRoot_WhenParentSpanIdIsAllZeros()
    {
        using var harness = new ReplayHarness();
        var entry = MakeEntry(SampleTraceId, "1111111111111111", parentSpanId: ZeroSpanId, name: "root");

        TraceReplayer.ReplayEntries(harness.Source, new[] { entry });

        var activity = Assert.Single(harness.Stopped);
        Assert.Equal(ZeroSpanId, activity.ParentSpanId.ToString());
        Assert.Equal(SampleTraceId, activity.TraceId.ToString());
    }

    [Fact]
    public void ReplayEntries_ShouldTreatEntryAsRoot_WhenParentSpanIdIsNotInBatch()
    {
        using var harness = new ReplayHarness();
        var orphan = MakeEntry(SampleTraceId, "2222222222222222", parentSpanId: "9999999999999999", name: "orphan");

        TraceReplayer.ReplayEntries(harness.Source, new[] { orphan });

        var activity = Assert.Single(harness.Stopped);
        Assert.Equal(SampleTraceId, activity.TraceId.ToString());
    }

    [Fact]
    public void ReplayEntries_ShouldHandleCycle_WithoutInfiniteLoop()
    {
        using var harness = new ReplayHarness();
        // Mutual parents: a → b, b → a. Neither is a root, so topological BFS
        // leaves both pending and the sweep code path must emit them.
        var a = MakeEntry(SampleTraceId, "1111111111111111", parentSpanId: "2222222222222222", name: "a");
        var b = MakeEntry(SampleTraceId, "2222222222222222", parentSpanId: "1111111111111111", name: "b");

        TraceReplayer.ReplayEntries(harness.Source, new[] { a, b });

        Assert.Equal(2, harness.Stopped.Count);
    }

    [Fact]
    public void ReplayEntries_ShouldKeepEntriesWithoutASpanId_WhenTheirParentIsInACycle()
    {
        using var harness = new ReplayHarness();
        // a and b are mutual parents, so neither is a root and the walk never reaches anything
        // hanging off them. c has no SpanId of its own and its parent is inside that cycle, so only
        // the sweep can emit it — requiring a SpanId there dropped it silently.
        var a = MakeEntry(SampleTraceId, "1111111111111111", parentSpanId: "2222222222222222", name: "a");
        var b = MakeEntry(SampleTraceId, "2222222222222222", parentSpanId: "1111111111111111", name: "b");
        var c = MakeEntry(SampleTraceId, spanId: null, parentSpanId: "1111111111111111", name: "c");

        TraceReplayer.ReplayEntries(harness.Source, new[] { a, b, c });

        Assert.Equal(3, harness.Stopped.Count);
        Assert.Single(harness.Stopped, activity => activity.OperationName == "c");
    }

    [Fact]
    public void ReplayEntries_ShouldRestoreTags()
    {
        using var harness = new ReplayHarness();
        var entry = MakeEntry(SampleTraceId, "1111111111111111", parentSpanId: null, name: "op");
        entry.Tags = ParseJson("""
            [
                { "Key": "foo", "Value": "bar" },
                { "Key": "baz", "Value": "qux" }
            ]
            """);

        TraceReplayer.ReplayEntries(harness.Source, new[] { entry });

        var activity = Assert.Single(harness.Stopped);
        Assert.Equal("bar", activity.GetTagItem("foo"));
        Assert.Equal("qux", activity.GetTagItem("baz"));
    }

    [Fact]
    public void BufferAndReplay_PreserveNativeGraphAfterChildFirstCompletion()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hierarchy-replay-" + Guid.NewGuid());
        try
        {
            using var producer = new ReplayHarness();
            using var exporter = new FileExporter(directory);
            using var recovery = producer.Source.StartActivity("Agent.Session", ActivityKind.Server)!;
            Activity first;
            Activity second;
            using (first = producer.Source.StartActivity("tool.get_command")!) { }
            using (second = producer.Source.StartActivity("tool.get_command")!) { }

            Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>([first, second], 2)));
            var file = Assert.Single(Directory.GetFiles(directory, TraceFileStore.PendingFilePattern));
            Assert.False(recovery.IsStopped);

            recovery.Dispose();
            Assert.Equal(ExportResult.Success, exporter.Export(new Batch<Activity>(recovery)));
            var entries = TraceFileFormat.Parse(File.ReadAllText(file));
            using var replay = new ReplayHarness();
            TraceReplayer.ReplayEntries(replay.Source, entries, TraceReplayProfile.ManagedLayer);
            var replayedRoot = Assert.Single(replay.Stopped, activity => activity.OperationName == "Agent.Session");
            Assert.Equal(default, replayedRoot.ParentSpanId);
            Assert.Equal(recovery.TraceId, replayedRoot.TraceId);
            Assert.Equal(3, replay.Stopped.Count);
            Assert.All(replay.Stopped.Where(activity => activity != replayedRoot), activity =>
            {
                Assert.Equal(replayedRoot.TraceId, activity.TraceId);
                Assert.Equal(replayedRoot.SpanId, activity.ParentSpanId);
            });
            Assert.NotEqual(first.SpanId, second.SpanId);
        }
        finally
        {
            DirectoryUtils.TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public void LegacyBuffer_IsNotLabelledWithSourceHierarchy()
    {
        using var producer = new ReplayHarness();
        using var replay = new ReplayHarness();
        using var legacy = producer.Source.StartActivity("Agent.Create");
        Assert.NotNull(legacy);
        var entry = MakeEntry(legacy.TraceId.ToHexString(), legacy.SpanId.ToHexString(), null, legacy.OperationName);

        TraceReplayer.ReplayEntries(replay.Source, [entry]);

        Assert.DoesNotContain(Assert.Single(replay.Stopped).TagObjects,
            tag => tag.Key.StartsWith("properties.hierarchy.", StringComparison.Ordinal));
    }

    [Fact]
    public void ReplayEntries_IsolatesAndRestoresUnrelatedAmbient()
    {
        using var harness = new ReplayHarness();
        using var ambient = new Activity("unrelated").Start();
        var entry = MakeEntry(SampleTraceId, "1111111111111111", null, "root");

        TraceReplayer.ReplayEntries(harness.Source, [entry], TraceReplayProfile.ManagedLayer);

        var replayed = Assert.Single(harness.Stopped);
        Assert.Equal(SampleTraceId, replayed.TraceId.ToString());
        Assert.Equal(default, replayed.ParentSpanId);
        Assert.Null(replayed.Parent);
        Assert.Same(ambient, Activity.Current);
    }

    [Fact]
    public void ManagedReplay_PreservesMissingParentInsteadOfInventingOne()
    {
        using var harness = new ReplayHarness();
        var entry = MakeEntry(SampleTraceId, "1111111111111111", "9999999999999999", "orphan");

        TraceReplayer.ReplayEntries(harness.Source, [entry], TraceReplayProfile.ManagedLayer);

        Assert.Equal("9999999999999999", Assert.Single(harness.Stopped).ParentSpanId.ToString());
    }

    [Fact]
    public void ReplayProfile_PreservesLegacyWireTypesAndRestoresManagedTypes()
    {
        var entry = MakeEntry(SampleTraceId, "1111111111111111", null, "root");
        entry.Tags = ParseJson("""[{"Key":"schemaVersion","Value":1},{"Key":"properties.count","Value":42}]""");
        using var legacy = new ReplayHarness();
        using var managed = new ReplayHarness();

        TraceReplayer.ReplayEntries(legacy.Source, [entry]);
        TraceReplayer.ReplayEntries(managed.Source, [entry], TraceReplayProfile.ManagedLayer);

        Assert.Equal("1", Assert.Single(legacy.Stopped).GetTagItem("schemaVersion"));
        Assert.Equal("42", legacy.Stopped[0].GetTagItem("properties.count"));
        Assert.Equal(1, Assert.Single(managed.Stopped).GetTagItem("schemaVersion"));
        Assert.Equal(42, managed.Stopped[0].GetTagItem("properties.count"));
    }

    [Fact]
    public void ReplayEntries_DoesNotDeduplicateOrJoinIdenticalSpanIdsFromDifferentTraces()
    {
        const string otherTraceId = "abcdef0123456789abcdef0123456789";
        using var harness = new ReplayHarness();
        var firstParent = MakeEntry(SampleTraceId, "1111111111111111", null, "first-parent");
        var firstChild = MakeEntry(SampleTraceId, "2222222222222222", firstParent.SpanId, "first-child");
        var otherParent = MakeEntry(otherTraceId, firstParent.SpanId!, null, "other-parent");
        var otherChild = MakeEntry(otherTraceId, firstChild.SpanId!, otherParent.SpanId, "other-child");

        TraceReplayer.ReplayEntries(harness.Source, [otherChild, firstChild, firstParent, otherParent]);

        Assert.Equal(4, harness.Stopped.Count);
        foreach (var trace in harness.Stopped.GroupBy(activity => activity.TraceId))
        {
            var parent = Assert.Single(trace, activity => activity.OperationName.EndsWith("-parent", StringComparison.Ordinal));
            var child = Assert.Single(trace, activity => activity.OperationName.EndsWith("-child", StringComparison.Ordinal));
            Assert.Equal(parent.SpanId, child.ParentSpanId);
        }
    }

    [Fact]
    public void ReplayEntries_ShouldRestoreTopLevelEntraObjectIdAsTelemetryTag()
    {
        using var harness = new ReplayHarness();
        var entry = MakeEntry(SampleTraceId, "1111111111111111", parentSpanId: null, name: "op");
        entry.EntraObjectId = "object-1";

        TraceReplayer.ReplayEntries(harness.Source, new[] { entry });

        var activity = Assert.Single(harness.Stopped);
        Assert.Equal("object-1", activity.GetTagItem(TelemetryTags.EntraObjectId));
    }

    [Fact]
    public void ReplayEntries_ShouldRestoreEvents()
    {
        using var harness = new ReplayHarness();
        var entry = MakeEntry(SampleTraceId, "1111111111111111", parentSpanId: null, name: "op");
        entry.Events = ParseJson("""
            [
                {
                    "Name": "evt-1",
                    "Timestamp": "2024-01-01T00:00:00.000Z",
                    "Attributes": [
                        { "Key": "k", "Value": "v" }
                    ]
                }
            ]
            """);

        TraceReplayer.ReplayEntries(harness.Source, new[] { entry });

        var activity = Assert.Single(harness.Stopped);
        var evt = Assert.Single(activity.Events);
        Assert.Equal("evt-1", evt.Name);
        var tag = Assert.Single(evt.Tags);
        Assert.Equal("k", tag.Key);
        Assert.Equal("v", tag.Value);
    }

    [Fact]
    public void ReplayEntries_ShouldRestoreStatus_WithoutDescription()
    {
        using var harness = new ReplayHarness();
        var entry = MakeEntry(SampleTraceId, "1111111111111111", parentSpanId: null, name: "op");
        entry.Status = "Error";

        TraceReplayer.ReplayEntries(harness.Source, new[] { entry });

        var activity = Assert.Single(harness.Stopped);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Null(activity.StatusDescription);
    }

    [Fact]
    public void ReplayEntries_ShouldRestoreDurationFromStartAndDurationFields()
    {
        using var harness = new ReplayHarness();
        var entry = MakeEntry(SampleTraceId, "1111111111111111", parentSpanId: null, name: "op");
        entry.Duration = 250.0;

        TraceReplayer.ReplayEntries(harness.Source, new[] { entry });

        var activity = Assert.Single(harness.Stopped);
        Assert.Equal(TimeSpan.FromMilliseconds(250.0), activity.Duration);
    }

    [Fact]
    public void ReplayEntries_ShouldUseOperationName_WhenPresent()
    {
        using var harness = new ReplayHarness();
        var entry = MakeEntry(SampleTraceId, "1111111111111111", parentSpanId: null, name: null);
        entry.OperationName = "fallback-op";

        TraceReplayer.ReplayEntries(harness.Source, new[] { entry });

        var activity = Assert.Single(harness.Stopped);
        Assert.Equal("fallback-op", activity.OperationName);
    }

    [Fact]
    public void ReplayEntries_ShouldUseUnknownName_WhenOperationNameIsNull()
    {
        using var harness = new ReplayHarness();
        var entry = MakeEntry(SampleTraceId, "1111111111111111", parentSpanId: null, name: null);
        entry.OperationName = null;

        TraceReplayer.ReplayEntries(harness.Source, new[] { entry });

        var activity = Assert.Single(harness.Stopped);
        Assert.Equal("unknown", activity.OperationName);
    }

    [Theory]
    [InlineData("Client", ActivityKind.Client)]
    [InlineData("Server", ActivityKind.Server)]
    [InlineData("Producer", ActivityKind.Producer)]
    [InlineData("Consumer", ActivityKind.Consumer)]
    [InlineData("Internal", ActivityKind.Internal)]
    [InlineData("garbage", ActivityKind.Internal)]
    public void ReplayEntries_ShouldRestoreKind_OrFallbackToInternal(string kindString, ActivityKind expected)
    {
        using var harness = new ReplayHarness();
        var entry = MakeEntry(SampleTraceId, "1111111111111111", parentSpanId: null, name: "op");
        entry.Kind = kindString;

        TraceReplayer.ReplayEntries(harness.Source, new[] { entry });

        var activity = Assert.Single(harness.Stopped);
        Assert.Equal(expected, activity.Kind);
    }

    private static TraceEntry MakeEntry(
        string? traceId,
        string? spanId,
        string? parentSpanId,
        string? name)
    {
        return new TraceEntry
        {
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = parentSpanId,
            OperationName = name,
            StartTime = "2024-01-01T00:00:00.0000000Z",
            Duration = 1.0,
            Status = "Unset",
            Kind = "Internal",
            Tags = ParseJson("[]"),
            Events = ParseJson("[]"),
            Resource = ParseJson("{}"),
        };
    }

    private static JsonElement ParseJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Owns a unique ActivitySource + ActivityListener pair so each test sees
    /// only the activities it produced, even when xunit runs tests in parallel.
    /// </summary>
    private sealed class ReplayHarness : IDisposable
    {
        private readonly ActivityListener _listener;

        public ReplayHarness()
        {
            var name = "azclips-replay-" + Guid.NewGuid().ToString("N");
            Source = new ActivitySource(name);
            Stopped = new List<Activity>();

            _listener = new ActivityListener
            {
                ShouldListenTo = src => src.Name == name,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = a => Stopped.Add(a),
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public ActivitySource Source { get; }

        public List<Activity> Stopped { get; }

        public void Dispose()
        {
            _listener.Dispose();
            Source.Dispose();
        }
    }
}
