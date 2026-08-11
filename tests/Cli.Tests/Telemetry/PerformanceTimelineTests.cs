// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Cli.Telemetry;

namespace Cli.Tests.Telemetry;

/// <summary>
/// PerformanceTimeline records startup phases before the telemetry stack exists and
/// replays them afterwards. These tests pin the two properties that make the replay
/// trustworthy: recorded durations survive, and recorded nesting survives.
/// </summary>
public class PerformanceTimelineTests
{
    [Fact]
    public void Replay_EmitsMeasuredPhaseWithItsRecordedDuration()
    {
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline();

        using (timeline.Measure("CLI.Performance.Container"))
        {
            Thread.Sleep(20);
        }

        timeline.Replay(telemetry);

        var container = Assert.Single(telemetry.Stopped, a => a.OperationName == "CLI.Performance.Container");
        // Sleep(20) can overshoot on Windows' ~15.6ms timer, so assert a floor, not equality.
        Assert.InRange(container.Duration.TotalMilliseconds, 15d, 5000d);
    }

    [Fact]
    public void Replay_WrapsPhasesInAnEnclosingPerformanceSpan()
    {
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline();

        using (timeline.Measure("CLI.Performance.Container"))
        {
        }

        timeline.Replay(telemetry);

        var container = Assert.Single(telemetry.Stopped, a => a.OperationName == "CLI.Performance.Container");
        Assert.Equal("CLI.Performance", container.Parent?.OperationName);
    }

    [Fact]
    public void Replay_SequentialMeasures_BecomeSiblings()
    {
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline();

        using (timeline.Measure("first"))
        {
        }

        using (timeline.Measure("second"))
        {
        }

        timeline.Replay(telemetry);

        var first = Assert.Single(telemetry.Stopped, a => a.OperationName == "first");
        var second = Assert.Single(telemetry.Stopped, a => a.OperationName == "second");
        Assert.Equal("CLI.Performance", first.Parent?.OperationName);
        Assert.Equal("CLI.Performance", second.Parent?.OperationName);
    }

    [Fact]
    public void Replay_NestedMeasures_BecomeNestedSpans()
    {
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline();

        using (timeline.Measure("outer"))
        using (timeline.Measure("inner"))
        {
        }

        timeline.Replay(telemetry);

        var outer = Assert.Single(telemetry.Stopped, a => a.OperationName == "outer");
        var inner = Assert.Single(telemetry.Stopped, a => a.OperationName == "inner");
        Assert.Equal("CLI.Performance", outer.Parent?.OperationName);
        Assert.Equal("outer", inner.Parent?.OperationName);
    }

    [Fact]
    public void Replay_WithNothingMeasured_EmitsNothing()
    {
        using var telemetry = new RecordingTelemetryService();

        new PerformanceTimeline().Replay(telemetry);

        Assert.Empty(telemetry.Stopped);
    }

    [Fact]
    public void Replay_WhenTelemetryIsDisabled_DoesNotThrow()
    {
        // With telemetry.enabled=false every StartActivity returns null. Replay must
        // tolerate that rather than NullReferenceException on the way out of az.
        var timeline = new PerformanceTimeline();

        using (timeline.Measure("CLI.Performance.Container"))
        {
        }

        Assert.Null(Record.Exception(() => timeline.Replay(new NullTelemetryService())));
    }

    [Fact]
    public void StartedAt_IsCapturedAtConstruction()
    {
        var beforeConstruction = DateTime.UtcNow;
        var timeline = new PerformanceTimeline();
        var afterConstruction = DateTime.UtcNow;

        Assert.InRange(timeline.StartedAt, beforeConstruction, afterConstruction);
    }

    [Fact]
    public void OutOfOrderDisposal_DoesNotCorruptParenting()
    {
        // Test for Finding 1: out-of-order disposal should not corrupt the stack.
        // Measure a, measure b nested in a, dispose a first (out of order), then dispose b,
        // then measure c normally. Assert c is a sibling of a (both under CLI.Performance),
        // not a child of a.
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline();

        var a = timeline.Measure("a");
        var b = timeline.Measure("b");

        // Dispose outer phase before inner phase (out of order)
        a.Dispose();
        b.Dispose();

        // Measure c after the out-of-order disposal
        using (timeline.Measure("c"))
        {
        }

        timeline.Replay(telemetry);

        var phaseA = Assert.Single(telemetry.Stopped, x => x.OperationName == "a");
        var phaseB = Assert.Single(telemetry.Stopped, x => x.OperationName == "b");
        var phaseC = Assert.Single(telemetry.Stopped, x => x.OperationName == "c");

        // c should be a sibling of a (both under CLI.Performance), not a child of a
        Assert.Equal("CLI.Performance", phaseA.Parent?.OperationName);
        Assert.Equal("CLI.Performance", phaseC.Parent?.OperationName);
        // b should be a child of a (nested inside a)
        Assert.Equal("a", phaseB.Parent?.OperationName);
    }

    [Fact]
    public void EnclosingSpanCoversAllPhases()
    {
        // Test for Finding 2: the enclosing span's end time should be the latest end
        // across all top-level phases, not just the last one registered.
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline();

        using (timeline.Measure("phase1"))
        {
            Thread.Sleep(10);
        }

        using (timeline.Measure("phase2"))
        {
            Thread.Sleep(20);
        }

        timeline.Replay(telemetry);

        var enclosing = Assert.Single(telemetry.Stopped, a => a.OperationName == "CLI.Performance");
        var phase1 = Assert.Single(telemetry.Stopped, a => a.OperationName == "phase1");
        var phase2 = Assert.Single(telemetry.Stopped, a => a.OperationName == "phase2");

        // Calculate end times as start time + duration
        var enclosingEnd = enclosing.StartTimeUtc + enclosing.Duration;
        var phase1End = phase1.StartTimeUtc + phase1.Duration;
        var phase2End = phase2.StartTimeUtc + phase2.Duration;

        // The enclosing span's end should be >= the end of both phases
        Assert.True(enclosingEnd >= phase1End, 
            $"Enclosing span end ({enclosingEnd}) should be >= phase1 end ({phase1End})");
        Assert.True(enclosingEnd >= phase2End, 
            $"Enclosing span end ({enclosingEnd}) should be >= phase2 end ({phase2End})");
    }

    [Fact]
    public void Replay_WithLeakedMeasureHandle_EmitsNonNegativeDuration()
    {
        // A caller that measures a phase but never disposes the handle should not
        // produce a span whose end precedes its start (End defaults to now, not MinValue).
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline();

        // Intentionally not disposed — leaked handle.
        var _ = timeline.Measure("leaked");

        Assert.Null(Record.Exception(() => timeline.Replay(telemetry)));

        var leaked = Assert.Single(telemetry.Stopped, a => a.OperationName == "leaked");
        Assert.True(leaked.Duration >= TimeSpan.Zero,
            $"Expected non-negative duration but got {leaked.Duration}");
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

        public Activity? StartRootActivity(DateTimeOffset startTime = default) =>
            StartActivity("Test.Activity.Root", ActivityKind.Server, startTime);

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
        public Activity? StartRootActivity(DateTimeOffset startTime = default) => null;
        public Activity? TagInvocation(CommandInvocation commandInvocation) => null;
        public Activity? AddTag(string propertyName, object? value, Activity? activity = null) => null;
        public void Dispose() { }
    }
}
