// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Runtime;
using Core.Telemetry;
using Microsoft.Azure.Azclips.Cmdlets.Accounts.Telemetry;

namespace Cmdlets.Tests;

/// <summary>
/// ImportTelemetry turns timestamps recorded during module import into a trace, once the
/// telemetry stack exists. These tests pin the two things that make the number
/// trustworthy: the import is labelled cold or warm correctly, and the root span reports
/// the recorded import window rather than the moment it happened to be emitted.
/// </summary>
public class ImportTelemetryTests
{
    [Fact]
    public void Emit_TagsTheRootWithTheImportKind()
    {
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline(ImportTelemetry.PerformanceSpanName);
        using (timeline.Measure(ImportTelemetry.ContainerSpanName))
        {
        }

        ImportTelemetry.Emit(telemetry, timeline, ImportTelemetry.Cold, DateTime.UtcNow);

        var root = Assert.Single(telemetry.Stopped, a => a.OperationName == "Test.Activity.Root");
        Assert.Equal(ImportTelemetry.Cold, root.GetTagItem(TelemetryTags.ImportKind));
    }

    [Fact]
    public void Emit_NestsTheContainerPhaseUnderThePerformanceSpan()
    {
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline(ImportTelemetry.PerformanceSpanName);
        using (timeline.Measure(ImportTelemetry.ContainerSpanName))
        {
            Thread.Sleep(20);
        }

        ImportTelemetry.Emit(telemetry, timeline, ImportTelemetry.Cold, DateTime.UtcNow);

        var container = Assert.Single(telemetry.Stopped, a => a.OperationName == ImportTelemetry.ContainerSpanName);
        Assert.Equal(ImportTelemetry.PerformanceSpanName, container.Parent?.OperationName);
        // Sleep(20) can overshoot on Windows' ~15.6ms timer, so assert a floor, not equality.
        Assert.InRange(container.Duration.TotalMilliseconds, 15d, 5000d);
    }

    [Fact]
    public void Emit_ClosesTheRootAtTheRecordedImportEnd_NotAtEmitTime()
    {
        // The root is created on a background thread after import finished. Disposing it
        // normally would report a duration stretching to emit time, inflated by thread
        // scheduling and by the telemetry construction being measured against it.
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline(ImportTelemetry.PerformanceSpanName);
        using (timeline.Measure(ImportTelemetry.ContainerSpanName))
        {
        }

        var importEnd = DateTime.UtcNow;
        Thread.Sleep(150); // stands in for the background scheduling + telemetry construction

        ImportTelemetry.Emit(telemetry, timeline, ImportTelemetry.Cold, importEnd);

        var root = Assert.Single(telemetry.Stopped, a => a.OperationName == "Test.Activity.Root");
        Assert.True(
            root.Duration.TotalMilliseconds < 100,
            $"root duration {root.Duration.TotalMilliseconds:N1}ms absorbed the 150ms emit delay");
    }

    [Fact]
    public void Emit_WhenTelemetryIsDisabled_DoesNotThrow()
    {
        var timeline = new PerformanceTimeline(ImportTelemetry.PerformanceSpanName);
        using (timeline.Measure(ImportTelemetry.ContainerSpanName))
        {
        }

        Assert.Null(Record.Exception(() =>
            ImportTelemetry.Emit(new NullTelemetryService(), timeline, ImportTelemetry.Cold, DateTime.UtcNow)));
    }

    [Fact]
    public void Emit_InvokesTagRoot_BeforeReplayingPhaseSpans()
    {
        // tagRoot must run before Replay: RecordingTelemetryService's activities are stopped
        // (and, in production, exported) as soon as each phase span is disposed inside Replay.
        // A tag applied only after Replay returns would never reach those already-exported
        // phase spans — this is exactly the bug class EmitInBackground's TenantId/DataBoundary
        // tagging depends on not having.
        using var telemetry = new RecordingTelemetryService();
        var timeline = new PerformanceTimeline(ImportTelemetry.PerformanceSpanName);
        using (timeline.Measure(ImportTelemetry.ContainerSpanName))
        {
        }

        ImportTelemetry.Emit(
            telemetry,
            timeline,
            ImportTelemetry.Cold,
            DateTime.UtcNow,
            root => root?.SetTag(TelemetryTags.DataBoundary, "EU"));

        var container = Assert.Single(telemetry.Stopped, a => a.OperationName == ImportTelemetry.ContainerSpanName);
        // container's direct parent is the "enclosing" span Replay creates (named after the
        // timeline itself); the DataBoundary tag lives one level further up, on the root.
        Assert.Equal("EU", container.Parent?.Parent?.GetTagItem(TelemetryTags.DataBoundary));
    }

    [Theory]
    [InlineData(1, ImportTelemetry.Cold)]
    [InlineData(2, ImportTelemetry.Warm)]
    [InlineData(3, ImportTelemetry.Warm)]
    [InlineData(99, ImportTelemetry.Warm)]
    public void ImportKindFor_IsColdOnlyForTheFirstImport(int importCount, string expected)
    {
        Assert.Equal(expected, ImportTelemetry.ImportKindFor(importCount));
    }

    [Fact]
    public void NextImportKind_ReturnsAKnownKind()
    {
        // The counter behind this is process-wide and cannot be reset, so the exact value
        // depends on what ran before. ImportKindFor covers the mapping; this only pins that
        // NextImportKind is wired to it.
        Assert.Contains(ImportTelemetry.NextImportKind(), new[] { ImportTelemetry.Cold, ImportTelemetry.Warm });
    }

    /// <summary>ITelemetryService backed by a real ActivitySource plus a listener, so
    /// spans are actually created and their stop events can be inspected.</summary>
    private sealed class RecordingTelemetryService : ITelemetryService
    {
        private readonly ActivitySource _source;
        private readonly ActivityListener _listener;

        public RecordingTelemetryService()
        {
            _source = new ActivitySource("import-telemetry-test-" + Guid.NewGuid().ToString("N"));
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
        public Activity? TagInvocation(CommandInvocation commandInvocation) => null;
        public Activity? AddTag(string propertyName, object? value, Activity? activity = null) => null;
        public void Dispose() { }
    }
}
