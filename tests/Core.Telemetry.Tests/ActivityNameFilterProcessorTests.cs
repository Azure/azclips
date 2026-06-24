// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;

namespace Core.Telemetry.Tests;

public class ActivityNameFilterProcessorTests : IDisposable
{
    private readonly ActivitySource _source;
    private readonly ActivityListener _listener;

    public ActivityNameFilterProcessorTests()
    {
        // Unique source name per fixture so parallel tests stay isolated.
        _source = new ActivitySource("cirrus-activity-name-test-" + Guid.NewGuid().ToString("N"));
        _listener = new ActivityListener
        {
            ShouldListenTo = src => src == _source,
            // AllDataAndRecorded gives us activities with the Recorded flag SET,
            // so we can observe the processor clearing it.
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
    }

    [Fact]
    public void OnEnd_AllowedActivity_LeavesRecordedFlagSet_AndDoesNotIncrementDroppedCount()
    {
        var processor = new DefaultActivityNameFilterProcessor(new AllowAllFilter());

        using var activity = _source.StartActivity("allowed-op")!;
        activity.Stop();
        Assert.True(activity.Recorded, "precondition: AllDataAndRecorded sampler must mark activity Recorded");

        processor.OnEnd(activity);

        Assert.True(activity.Recorded);
        Assert.Equal(0, processor.DroppedCount);
    }

    [Fact]
    public void OnEnd_DisallowedActivity_ClearsRecordedFlag_AndIncrementsDroppedCount()
    {
        var processor = new DefaultActivityNameFilterProcessor(new DenyAllFilter());

        using var activity = _source.StartActivity("disallowed-op")!;
        activity.Stop();
        Assert.True(activity.Recorded, "precondition: AllDataAndRecorded sampler must mark activity Recorded");

        processor.OnEnd(activity);

        Assert.False(activity.Recorded);
        Assert.Equal(1, processor.DroppedCount);
    }

    [Fact]
    public void OnEnd_MultipleDisallowed_AccumulatesDroppedCount()
    {
        var processor = new DefaultActivityNameFilterProcessor(new DenyAllFilter());

        for (var i = 0; i < 5; i++)
        {
            using var activity = _source.StartActivity($"op-{i}")!;
            activity.Stop();
            processor.OnEnd(activity);
        }

        Assert.Equal(5, processor.DroppedCount);
    }

    [Fact]
    public void OnEnd_LeavesActivityMetadataUntouched_WhenDropping()
    {
        var processor = new DefaultActivityNameFilterProcessor(new DenyAllFilter());

        using var activity = _source.StartActivity("preserve-me", ActivityKind.Server)!;
        var originalTraceId = activity.TraceId;
        var originalSpanId = activity.SpanId;
        var originalOperationName = activity.OperationName;
        var originalKind = activity.Kind;
        activity.SetTag("some.tag", "value");
        activity.SetStatus(ActivityStatusCode.Error, "bad");
        activity.Stop();

        processor.OnEnd(activity);

        // Only the Recorded part of TraceFlags is touched; everything else survives.
        Assert.Equal(originalTraceId, activity.TraceId);
        Assert.Equal(originalSpanId, activity.SpanId);
        Assert.Equal(originalOperationName, activity.OperationName);
        Assert.Equal(originalKind, activity.Kind);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("bad", activity.StatusDescription);
        Assert.Equal("value", activity.GetTagItem("some.tag"));
        Assert.False(activity.Recorded);
    }

    [Fact]
    public void OnEnd_NullActivity_IsNoOp()
    {
        var processor = new DefaultActivityNameFilterProcessor(new DenyAllFilter());

        processor.OnEnd(null!);

        Assert.Equal(0, processor.DroppedCount);
    }

    [Fact]
    public void Constructor_ThrowsOnNullFilter()
    {
        Assert.Throws<ArgumentNullException>(() => new DefaultActivityNameFilterProcessor(null!));
    }

    [Fact]
    public void OnEnd_UsesActualDefaultFilter_AllowsKnownRootName_DropsUnknownName()
    {
        var processor = new DefaultActivityNameFilterProcessor(new DefaultTelemetryActivityNameFilter());

        using var allowed = _source.StartActivity("CLI.Activity.Root")!;
        allowed.Stop();
        processor.OnEnd(allowed);

        using var dropped = _source.StartActivity("SomeUnknownActivity")!;
        dropped.Stop();
        processor.OnEnd(dropped);

        Assert.True(allowed.Recorded);
        Assert.False(dropped.Recorded);
        Assert.Equal(1, processor.DroppedCount);
    }

    private sealed class AllowAllFilter : ITelemetryActivityNameFilter
    {
        public bool IsAllowed(string? activityName) => true;
    }

    private sealed class DenyAllFilter : ITelemetryActivityNameFilter
    {
        public bool IsAllowed(string? activityName) => false;
    }
}
