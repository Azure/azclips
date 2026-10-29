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
        activity.SetTag(TelemetryTags.CommandName, "Get-AzAccount");
        activity.SetTag(TelemetryTags.CommandParameters, "--name test");
        activity.SetTag(TelemetryTags.CommandOutputType, "json");
        activity.SetTag(TelemetryTags.SubscriptionId, Guid.NewGuid().ToString("N"));
        activity.SetTag(TelemetryTags.TenantId, Guid.NewGuid().ToString("N"));
        activity.SetTag(TelemetryTags.ProductName, "CLI");
        activity.SetTag(TelemetryTags.ProductVersion, "1.0.0");
        activity.SetTag(TelemetryTags.Locale, "en-US");
        activity.SetTag(TelemetryTags.InstallationId, "1234567890");
        activity.SetTag(TelemetryTags.ExceptionType, "exception");
        activity.SetTag(TelemetryTags.EventName, "processasync");
        activity.SetTag(TelemetryTags.EntraObjectId, Guid.NewGuid().ToString("N"));
        activity.SetTag(TelemetryTags.AuthDurationTotalMs, "29");
        activity.SetTag(TelemetryTags.AuthStatusCode, "200");
        activity.SetTag(TelemetryTags.AuthType, "browser");
        activity.SetTag(TelemetryTags.ClientRequestId, Guid.NewGuid().ToString("N"));
        activity.SetTag(TelemetryTags.CommandSpanId, "123");
        activity.SetTag(TelemetryTags.CommandTraceId, "456");
        activity.SetTag(TelemetryTags.DataBoundary, "Global");
        activity.SetTag(TelemetryTags.DistributionId, "1");
        activity.SetTag(TelemetryTags.DistributionName, "Ubuntu");
        activity.SetTag(TelemetryTags.DotNetVersion, "10.0.0");
        activity.SetTag(TelemetryTags.CommandExtensionVersion, "1.0.0");
        activity.SetTag(TelemetryTags.OperatingSystemType, "Linux");
        activity.SetTag(TelemetryTags.OperatingSystemVersion, "1.0.0");
        activity.SetTag(TelemetryTags.PowerShellVersion, "7.6");
        activity.SetTag(TelemetryTags.ShellType, "pwsh");
        activity.SetTag(TelemetryTags.CommandExtensionName, "Azclips.Login");

        // dropped
        activity.SetTag("http.url", "https://example.com/subs/abcd");
        activity.SetTag("server.address", "https://example.com/subs/abcd");
        activity.SetTag("http.target", "https://example.com/subs/abcd");
        activity.SetTag("exception.message", "exception");
        activity.SetTag("exception.stacktrace", "exception");
        activity.SetTag("otel.status_description", "error");                
        activity.SetTag("unknown.tag", "unknown");

        activity.Stop();

        processor.OnEnd(activity);

        var keys = TagKeys(activity);
        Assert.Contains(TelemetryTags.CommandName, keys);
        Assert.Contains(TelemetryTags.CommandParameters, keys);
        Assert.Contains(TelemetryTags.CommandExtensionName, keys);
        Assert.Contains(TelemetryTags.CommandOutputType, keys);
        Assert.Contains(TelemetryTags.SubscriptionId, keys);
        Assert.Contains(TelemetryTags.TenantId, keys);
        Assert.Contains(TelemetryTags.ProductName, keys);
        Assert.Contains(TelemetryTags.ProductVersion, keys);
        Assert.Contains(TelemetryTags.Locale, keys);
        Assert.Contains(TelemetryTags.InstallationId, keys);
        Assert.Contains(TelemetryTags.ExceptionType, keys);
        Assert.Contains(TelemetryTags.EventName, keys);
        Assert.Contains(TelemetryTags.EntraObjectId, keys);
        Assert.Contains(TelemetryTags.AuthDurationTotalMs, keys);
        Assert.Contains(TelemetryTags.AuthStatusCode, keys);
        Assert.Contains(TelemetryTags.AuthType, keys);
        Assert.Contains(TelemetryTags.ClientRequestId, keys);
        Assert.Contains(TelemetryTags.CommandSpanId, keys);
        Assert.Contains(TelemetryTags.CommandTraceId, keys);
        Assert.Contains(TelemetryTags.DataBoundary, keys);
        Assert.Contains(TelemetryTags.DistributionId, keys);
        Assert.Contains(TelemetryTags.DistributionName, keys);
        Assert.Contains(TelemetryTags.DotNetVersion, keys);
        Assert.Contains(TelemetryTags.CommandExtensionVersion, keys);
        Assert.Contains(TelemetryTags.OperatingSystemType, keys);
        Assert.Contains(TelemetryTags.OperatingSystemVersion, keys);
        Assert.Contains(TelemetryTags.PowerShellVersion, keys);
        Assert.Contains(TelemetryTags.ShellType, keys);

        Assert.DoesNotContain("http.url", keys);
        Assert.DoesNotContain("server.address", keys);
        Assert.DoesNotContain("http.target", keys);
        Assert.DoesNotContain("exception.message", keys);
        Assert.DoesNotContain("exception.stacktrace", keys);
        Assert.DoesNotContain("otel.status_description", keys);
        Assert.DoesNotContain("module.name", keys);
        Assert.DoesNotContain("module.version", keys);
        Assert.DoesNotContain("unknown.tag", keys);

        Assert.Equal(7, processor.DroppedCount);
    }

    [Fact]
    public void OnEnd_DropsNothing_WhenAllTagsAllowed()
    {
        var processor = new DefaultTelemetryPropertyFilterProcessor(_filter);

        using var activity = _source.StartActivity("op")!;
        activity.SetTag(TelemetryTags.CommandName, "any");
        activity.SetTag(TelemetryTags.CommandExtensionName, "any");
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
