// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Core.Telemetry.Tests;

/// <summary>
/// PerformanceTimeline records startup phases before the telemetry stack exists and
/// replays them afterwards. These tests pin the properties that make the replay
/// trustworthy: recorded durations survive, and recorded nesting survives.
/// </summary>
public class PerformanceTimelineTests
{
    private const string Enclosing = "CLI.Performance";

    [Fact]
    public void Replay_EmitsMeasuredPhaseWithItsRecordedDuration()
    {
        using var telemetry = new RecordingTelemetryService();
        // A fake clock keeps the recorded duration exact; a real one would also count however
        // long a loaded CI runner takes to reschedule this thread, which has exceeded 5 s.
        var clock = new FakeTimeProvider();
        var timeline = new PerformanceTimeline(Enclosing, clock);

        using (timeline.Measure("CLI.Performance.Container"))
        {
            clock.Advance(TimeSpan.FromMilliseconds(20));
        }

        timeline.Replay(telemetry);

        var container = Assert.Single(telemetry.Stopped, a => a.OperationName == "CLI.Performance.Container");
        Assert.Equal(TimeSpan.FromMilliseconds(20), container.Duration);
    }

    [Fact]
    public void Replay_WrapsPhasesInAnEnclosingPerformanceSpan()
    {
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline(Enclosing, TimeProvider.System);

        using (timeline.Measure("CLI.Performance.Container"))
        {
        }

        timeline.Replay(telemetry);

        var container = Assert.Single(telemetry.Stopped, a => a.OperationName == "CLI.Performance.Container");
        Assert.Equal("CLI.Performance", container.Parent?.OperationName);
    }

    [Fact]
    public void Replay_UsesTheEnclosingSpanNameGivenToTheConstructor()
    {
        // The CLI and the PowerShell module share this type; each supplies its own
        // "{ClientType}.Performance" name. A hard-coded name would silently mislabel one.
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline("PowerShell.Performance", TimeProvider.System);

        using (timeline.Measure("PowerShell.Performance.Container"))
        {
        }

        timeline.Replay(telemetry);

        Assert.Single(telemetry.Stopped, a => a.OperationName == "PowerShell.Performance");
        Assert.DoesNotContain(telemetry.Stopped, a => a.OperationName == "CLI.Performance");
    }

    [Fact]
    public void Replay_SequentialMeasures_BecomeSiblings()
    {
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline(Enclosing, TimeProvider.System);

        using (timeline.Measure("first"))
        {
        }

        using (timeline.Measure("second"))
        {
        }

        timeline.Replay(telemetry);

        var first = Assert.Single(telemetry.Stopped, a => a.OperationName == "first");
        var second = Assert.Single(telemetry.Stopped, a => a.OperationName == "second");
        Assert.Equal(Enclosing, first.Parent?.OperationName);
        Assert.Equal(Enclosing, second.Parent?.OperationName);
    }

    [Fact]
    public void Replay_NestedMeasures_BecomeNestedSpans()
    {
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline(Enclosing, TimeProvider.System);

        using (timeline.Measure("outer"))
        using (timeline.Measure("inner"))
        {
        }

        timeline.Replay(telemetry);

        var outer = Assert.Single(telemetry.Stopped, a => a.OperationName == "outer");
        var inner = Assert.Single(telemetry.Stopped, a => a.OperationName == "inner");
        Assert.Equal(Enclosing, outer.Parent?.OperationName);
        Assert.Equal("outer", inner.Parent?.OperationName);
    }

    [Fact]
    public void Replay_WithNothingMeasured_EmitsNothing()
    {
        using var telemetry = new RecordingTelemetryService();

        new PerformanceTimeline(Enclosing, TimeProvider.System).Replay(telemetry);

        Assert.Empty(telemetry.Stopped);
    }

    [Fact]
    public void Replay_WhenTelemetryIsDisabled_DoesNotThrow()
    {
        // With telemetry.enabled=false every StartActivity returns null. Replay must
        // tolerate that rather than NullReferenceException on the way out.
        var timeline = new PerformanceTimeline(Enclosing, TimeProvider.System);

        using (timeline.Measure("CLI.Performance.Container"))
        {
        }

        Assert.Null(Record.Exception(() => timeline.Replay(new NullTelemetryService())));
    }

    [Fact]
    public void StartedAt_IsCapturedAtConstruction()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var timeline = new PerformanceTimeline(Enclosing, clock);
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), timeline.StartedAt);
        // A non-UTC kind would be read as local time when backdating the root activity.
        Assert.Equal(DateTimeKind.Utc, timeline.StartedAt.Kind);
    }

    [Fact]
    public void OutOfOrderDisposal_DoesNotCorruptParenting()
    {
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline(Enclosing, TimeProvider.System);

        var a = timeline.Measure("a");
        var b = timeline.Measure("b");
        a.Dispose();
        b.Dispose();

        using (timeline.Measure("c"))
        {
        }

        timeline.Replay(telemetry);

        var c = Assert.Single(telemetry.Stopped, x => x.OperationName == "c");
        Assert.Equal(Enclosing, c.Parent?.OperationName);
    }

    [Fact]
    public void EnclosingSpanCoversAllPhases()
    {
        using var telemetry = new RecordingTelemetryService();
        var clock = new FakeTimeProvider();
        var timeline = new PerformanceTimeline(Enclosing, clock);

        using (timeline.Measure("first"))
        {
            clock.Advance(TimeSpan.FromMilliseconds(10));
        }

        using (timeline.Measure("second"))
        {
            clock.Advance(TimeSpan.FromMilliseconds(10));
        }

        timeline.Replay(telemetry);

        var enclosing = Assert.Single(telemetry.Stopped, a => a.OperationName == Enclosing);
        var first = Assert.Single(telemetry.Stopped, a => a.OperationName == "first");
        var second = Assert.Single(telemetry.Stopped, a => a.OperationName == "second");
        var enclosingEnd = enclosing.StartTimeUtc + enclosing.Duration;
        Assert.True(enclosingEnd >= first.StartTimeUtc + first.Duration);
        Assert.True(enclosingEnd >= second.StartTimeUtc + second.Duration);
    }

    [Fact]
    public void Replay_LeakedMeasureHandle_DoesNotEmitNegativeDuration()
    {
        using var telemetry = new RecordingTelemetryService();
        var clock = new FakeTimeProvider();
        var timeline = new PerformanceTimeline(Enclosing, clock);

        // Deliberately never disposed.
        timeline.Measure("leaked");
        clock.Advance(TimeSpan.FromMilliseconds(5));

        timeline.Replay(telemetry);

        // A handle that was never disposed ends at replay time.
        var leaked = Assert.Single(telemetry.Stopped, a => a.OperationName == "leaked");
        Assert.Equal(TimeSpan.FromMilliseconds(5), leaked.Duration);
    }

    [Fact]
    public void Replay_EmitsTelemetryHandoffPhaseWithRecordedDuration()
    {
        using var telemetry = new RecordingTelemetryService();
        var clock = new FakeTimeProvider();
        var timeline = new PerformanceTimeline(Enclosing, clock);

        using (timeline.Measure("CLI.Performance.Telemetry"))
        {
            using var root = telemetry.StartRootActivity(timeline.StartedAt);
            clock.Advance(TimeSpan.FromMilliseconds(20));
            timeline.Replay(telemetry);
        }

        var handoff = Assert.Single(
            telemetry.Stopped,
            a => a.OperationName == "CLI.Performance.Telemetry");

        Assert.Equal(TimeSpan.FromMilliseconds(20), handoff.Duration);
    }

    /// <summary>ITelemetryService backed by a real ActivitySource plus a listener, so
    /// spans are actually created and their stop events can be inspected.</summary>
    private sealed class RecordingTelemetryService : ITelemetryService
    {
        private readonly ActivitySource _source;
        private readonly ActivityListener _listener;

        public RecordingTelemetryService()
        {
            _source = new ActivitySource("perf-timeline-test-" + Guid.NewGuid().ToString("N"));
            _listener = new ActivityListener
            {
                ShouldListenTo = src => src == _source,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
                ActivityStopped = Stopped.Add,
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public List<Activity> Stopped { get; } = new();

        public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal, DateTimeOffset startTime = default) =>
            _source.StartActivity(name, kind, parentContext: default, tags: null, links: null, startTime: startTime);

        public Activity? StartRootActivity(DateTimeOffset startTime = default, string? name = null) =>
            StartActivity(name ?? "Test.Activity.Root", ActivityKind.Server, startTime);

        public TelemetryActivityScope? StartActivityWithParent(string name, ActivityContext parentContext,
            ActivityKind kind = ActivityKind.Internal, DateTimeOffset startTime = default) =>
            parentContext == default
                ? StartIndependentRootActivity(name, kind, startTime)
                : (_source.StartActivity(name, kind, parentContext, startTime: startTime) is { } started ? new TelemetryActivityScope(started) : null);

        public TelemetryActivityScope? StartIndependentRootActivity(string name, ActivityKind kind = ActivityKind.Server,
            DateTimeOffset startTime = default, bool activate = true) =>
            global::Tests.Common.Stubs.StubTelemetryService.CreateIndependentScope(_source, name, kind, startTime, activate);

        public Activity? TagInvocation(CommandInvocation commandInvocation) => Activity.Current;

        public Activity? AddTag(string propertyName, object? value, Activity? activity = null) => activity;

        public void Dispose()
        {
            _listener.Dispose();
            _source.Dispose();
        }
    }

    /// <summary>Stands in for a telemetry service with telemetry.enabled=false.</summary>
    private sealed class NullTelemetryService : ITelemetryService
    {
        public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal, DateTimeOffset startTime = default) => null;
        public Activity? StartRootActivity(DateTimeOffset startTime = default, string? name = null) => null;
        public TelemetryActivityScope? StartActivityWithParent(string name, ActivityContext parentContext,
            ActivityKind kind = ActivityKind.Internal, DateTimeOffset startTime = default) => null;
        public TelemetryActivityScope? StartIndependentRootActivity(string name, ActivityKind kind = ActivityKind.Server,
            DateTimeOffset startTime = default, bool activate = true) => null;
        public Activity? TagInvocation(CommandInvocation commandInvocation) => null;
        public Activity? AddTag(string propertyName, object? value, Activity? activity = null) => null;
        public void Dispose() { }
    }
}
