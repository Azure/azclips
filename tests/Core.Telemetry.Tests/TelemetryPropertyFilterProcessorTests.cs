// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry;

namespace Core.Telemetry.Tests;

public class TelemetryPropertyFilterProcessorTests : IDisposable
{
    private readonly ActivitySource _source;
    private readonly ActivityListener _listener;
    private readonly ITelemetryPropertyFilter _filter = new DefaultTelemetryPropertyFilter();

    public TelemetryPropertyFilterProcessorTests()
    {
        // Unique source name per fixture so parallel tests stay isolated.
        _source = new ActivitySource("azclips-filter-test-" + Guid.NewGuid().ToString("N"));
        _listener = new ActivityListener
        {
            ShouldListenTo = src => src == _source,
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
    public void OnEnd_DropsTagsNotInAllowlist_AndKeepsAllowedTags()
    {
        var processor = new DefaultTelemetryPropertyFilterProcessor(_filter);

        using var activity = _source.StartActivity("op")!;
        activity.SetTag(TelemetryTags.Command, "Get-AzAccount");           // allowed
        activity.SetTag("http.url", "https://example.com/subs/abcd");           // dropped
        activity.SetTag("subscriptionId", "guid-1234");                         // dropped
        activity.SetTag(TelemetryTags.ModuleName, "Azclips.Login");             // allowed
        activity.Stop();

        processor.OnEnd(activity);

        var keys = TagKeys(activity);
        Assert.Contains(TelemetryTags.Command, keys);
        Assert.Contains(TelemetryTags.ModuleName, keys);
        Assert.DoesNotContain("http.url", keys);
        Assert.Equal(1, processor.DroppedCount);
    }

    [Fact]
    public void OnEnd_DropsNothing_WhenAllTagsAllowed()
    {
        var processor = new DefaultTelemetryPropertyFilterProcessor(_filter);

        using var activity = _source.StartActivity("op")!;
        activity.SetTag(TelemetryTags.Command, "any");
        activity.SetTag(TelemetryTags.ModuleName, "any");
        activity.Stop();

        processor.OnEnd(activity);

        Assert.Equal(0, processor.DroppedCount);
        Assert.Equal(2, TagKeys(activity).Count);
    }

    [Fact]
    public void OnEnd_DropsEverything_WhenNoTagAllowed()
    {
        var processor = new DefaultTelemetryPropertyFilterProcessor(_filter);

        using var activity = _source.StartActivity("op")!;
        activity.SetTag("http.url", "https://example.com");
        activity.SetTag("exception.message", "boom");
        activity.Stop();

        processor.OnEnd(activity);

        Assert.Equal(2, processor.DroppedCount);
        Assert.Empty(TagKeys(activity));
    }

    [Fact]
    public void OnEnd_PreservesStructuredActivityMetadata_AndClearsStatusDescription()
    {
        var processor = new DefaultTelemetryPropertyFilterProcessor(_filter);

        using var activity = _source.StartActivity("preserve-me", ActivityKind.Server)!;
        var originalTraceId = activity.TraceId;
        var originalSpanId = activity.SpanId;
        var originalOperationName = activity.OperationName;
        var originalKind = activity.Kind;

        activity.SetTag("http.url", "https://example.com"); // will be dropped
        activity.SetStatus(ActivityStatusCode.Error, "bad");
        activity.Stop();

        processor.OnEnd(activity);

        Assert.Equal(originalTraceId, activity.TraceId);
        Assert.Equal(originalSpanId, activity.SpanId);
        Assert.Equal(originalOperationName, activity.OperationName);
        Assert.Equal(originalKind, activity.Kind);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Null(activity.StatusDescription);
    }

    [Fact]
    public void OnEnd_NullActivity_IsNoOp()
    {
        var processor = new DefaultTelemetryPropertyFilterProcessor(_filter);

        // Should not throw — processors may receive null in pathological pipelines.
        processor.OnEnd(null!);
        Assert.Equal(0, processor.DroppedCount);
    }

    [Fact]
    public void Constructor_ThrowsOnNullFilter()
    {
        Assert.Throws<ArgumentNullException>(() => new DefaultTelemetryPropertyFilterProcessor(null!));
    }

    private static HashSet<string> TagKeys(Activity activity) =>
        new(activity.TagObjects.Select(t => t.Key));
}
