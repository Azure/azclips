// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Trace;

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
    public void ReplayEntries_LinksChildToParent_WhenTheyCameFromDifferentFiles()
    {
        using var harness = new ReplayHarness();
        // The CLI and agent write separate trace files. Without the spanIdMap remap the
        // replayer falls back to ActivitySpanId.CreateRandom() and the agent subtree
        // detaches from the recovery span with no error.
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
        // Root entries get a random parent SpanId so the W3C context stays valid;
        // it must not be the all-zero placeholder from the input.
        Assert.NotEqual(ZeroSpanId, activity.ParentSpanId.ToString());
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
    public void ReplayEntries_ShouldRestoreStatus_AndDescription()
    {
        using var harness = new ReplayHarness();
        var entry = MakeEntry(SampleTraceId, "1111111111111111", parentSpanId: null, name: "op");
        entry.Status = "Error";
        entry.StatusDescription = "boom";

        TraceReplayer.ReplayEntries(harness.Source, new[] { entry });

        var activity = Assert.Single(harness.Stopped);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("boom", activity.StatusDescription);
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
    public void ReplayEntries_ShouldUseOperationName_WhenDisplayNameIsNull()
    {
        using var harness = new ReplayHarness();
        var entry = MakeEntry(SampleTraceId, "1111111111111111", parentSpanId: null, name: null);
        entry.OperationName = "fallback-op";

        TraceReplayer.ReplayEntries(harness.Source, new[] { entry });

        var activity = Assert.Single(harness.Stopped);
        Assert.Equal("fallback-op", activity.OperationName);
    }

    [Fact]
    public void ReplayEntries_ShouldUseUnknownName_WhenDisplayNameAndOperationNameAreNull()
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
        string spanId,
        string? parentSpanId,
        string? name)
    {
        return new TraceEntry
        {
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = parentSpanId,
            DisplayName = name,
            OperationName = name,
            StartTime = "2024-01-01T00:00:00.0000000Z",
            Duration = 1.0,
            Status = "Unset",
            StatusDescription = null,
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
