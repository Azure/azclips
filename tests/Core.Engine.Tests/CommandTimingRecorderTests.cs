// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Runtime;

namespace Core.Engine.Tests;

public class CommandTimingRecorderTests
{
    [Fact]
    public void Capture_UsesMonotonicOffsetsDespiteWallClockChanges()
    {
        var clock = new ManualClock();
        var recorder = new CommandTimingRecorder(clock);
        clock.Advance(10);
        using (recorder.MeasureLocal("phase"))
        {
            clock.Advance(25);
            clock.UtcNow = clock.UtcNow.AddDays(-1);
        }
        clock.Advance(5);

        var snapshot = recorder.Capture();
        var phase = Assert.Single(snapshot.Measurements);
        Assert.Equal(TimeSpan.FromMilliseconds(10), phase.Start);
        Assert.Equal(TimeSpan.FromMilliseconds(25), phase.Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(40), snapshot.Elapsed);
        Assert.Equal(TimeSpan.FromMilliseconds(15), snapshot.Uninstrumented);
        Assert.True(phase.IsComplete);
    }

    [Fact]
    public void Capture_UsesIntervalUnionForNestedAndOverlappingPhases()
    {
        var clock = new ManualClock();
        var recorder = new CommandTimingRecorder(clock);
        clock.Advance(5);
        var outer = recorder.MeasureLocal("outer");
        clock.Advance(5);
        var inner = recorder.MeasureLocal("inner");
        clock.Advance(5);
        outer.Dispose();
        clock.Advance(10);
        inner.Dispose();
        inner.Dispose();
        clock.Advance(5);

        var snapshot = recorder.Capture();
        Assert.Equal(TimeSpan.FromMilliseconds(10), snapshot.Uninstrumented);
        Assert.Equal([0, 1], snapshot.Measurements.Select(p => p.Depth));
        Assert.Equal(TimeSpan.FromMilliseconds(10), snapshot.Measurements[0].Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(15), snapshot.Measurements[1].Duration);

        using (recorder.MeasureLocal("sibling"))
        {
            clock.Advance(5);
        }
        Assert.Equal(0, recorder.Capture().Measurements[2].Depth);
    }

    [Fact]
    public async Task Capture_PreservesAsyncSiblingParentingAndRepeatedNames()
    {
        var recorder = new CommandTimingRecorder();
        using (recorder.MeasureLocal("parent"))
        {
            await Task.WhenAll(Child(), Child());
        }

        Assert.Equal([0, 1, 1], recorder.Capture().Measurements.Select(p => p.Depth));
        Assert.All(recorder.Capture().Measurements, p => Assert.True(p.IsComplete));

        async Task Child()
        {
            using var phase = recorder.MeasureLocal("child");
            await Task.Yield();
        }
    }

    [Fact]
    public void Capture_IncludesOpenPhasesAndRemainsImmutable()
    {
        var clock = new ManualClock();
        var recorder = new CommandTimingRecorder(clock);
        using var phase = recorder.MeasureLocal("open");
        clock.Advance(5);
        var snapshot = recorder.Capture();
        clock.Advance(10);
        phase.Dispose();

        Assert.False(Assert.Single(snapshot.Measurements).IsComplete);
        Assert.Equal(TimeSpan.FromMilliseconds(5), snapshot.Elapsed);
        Assert.Equal(TimeSpan.FromMilliseconds(15), Assert.Single(recorder.Capture().Measurements).Duration);
    }

    [Fact]
    public void LocalMeasurements_DoNotCreateActivitiesOrChangeAmbientActivity()
    {
        var stopped = new List<Activity>();
        using var source = new ActivitySource("command-timing-recorder-test");
        using var listener = new ActivityListener
        {
            ShouldListenTo = candidate => ReferenceEquals(candidate, source),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
            ActivityStopped = stopped.Add,
        };
        ActivitySource.AddActivityListener(listener);
        using var root = source.StartActivity("existing telemetry");
        Assert.NotNull(root);
        var recorder = new CommandTimingRecorder();

        using (recorder.MeasureLocal("command"))
        using (recorder.MeasureLocal("output"))
        {
            Assert.Same(root, Activity.Current);
        }

        Assert.Same(root, Activity.Current);
        Assert.DoesNotContain(stopped, activity => activity.OperationName is "command" or "output");
        Assert.Equal(2, recorder.Capture().Measurements.Count);
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
        public override long GetTimestamp() => _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public void Advance(int milliseconds) => _timestamp += TimeSpan.FromMilliseconds(milliseconds).Ticks;
    }
}
