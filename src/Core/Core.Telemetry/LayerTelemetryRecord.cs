// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>An activity's immutable identity; deadline capture never reads the live activity.</summary>
internal sealed class LayerTelemetryRecord
{
    private static readonly string[] InvocationMetadataKeys =
    [
        TelemetryTags.CommandTraceId, TelemetryTags.CommandSpanId, TelemetryTags.Command,
        TelemetryTags.TenantId, TelemetryTags.SubscriptionId,
    ];

    private static readonly string[] RootIdentityKeys =
    [
        TelemetryTags.InstallationId, TelemetryTags.ProductName, TelemetryTags.ProductVersion,
        TelemetryTags.SchemaVersion, TelemetryTags.TelemetryVersion, TelemetryTags.ClientType,
    ];

    internal static readonly string[] RequiredMetadataKeys =
    [
        TelemetryTags.InstallationId, TelemetryTags.ProductName, TelemetryTags.ProductVersion,
        TelemetryTags.SchemaVersion, TelemetryTags.TelemetryVersion, TelemetryTags.ClientType,
        TelemetryTags.EventName, TelemetryLayerTags.Schema, TelemetryLayerTags.ServiceName,
        TelemetryLayerTags.InvocationId, TelemetryTags.CommandTraceId, TelemetryTags.CommandSpanId,
        TelemetryTags.Command, TelemetryTags.TenantId, TelemetryTags.SubscriptionId,
        TelemetryTags.DataBoundary, TelemetryLayerTags.Completion,
        TelemetryLayerTags.PendingProducers, TelemetryLayerTags.AbandonedSpans,
    ];

    private readonly TelemetryLayer _layer;
    private readonly TelemetryInvocationContext _context;
    private readonly Dictionary<string, JsonElement> _initialTags;
    private readonly Dictionary<string, JsonElement> _metadata;
    private readonly JsonElement _resource;
    private readonly long _createdTimestamp;
    private readonly TimeSpan _initialAge;
    private readonly string _traceId;
    private readonly string _spanId;
    private readonly string _parentSpanId;
    private readonly string _operationName;
    private readonly string _displayName;
    private readonly string _startTime;
    private readonly string _kind;

    internal LayerTelemetryRecord(
        TelemetryLayer layer, TelemetryInvocationContext context, Activity activity, TimeProvider timeProvider)
    {
        _layer = layer;
        _context = context;
        _traceId = activity.TraceId.ToString();
        _spanId = activity.SpanId.ToString();
        _parentSpanId = activity.ParentSpanId == default ? string.Empty : activity.ParentSpanId.ToString();
        _operationName = activity.OperationName;
        _displayName = activity.DisplayName;
        _startTime = activity.StartTimeUtc.ToString("O", CultureInfo.InvariantCulture);
        _kind = activity.Kind.ToString();
        _createdTimestamp = timeProvider.GetTimestamp();
        _initialAge = timeProvider.GetUtcNow().UtcDateTime - activity.StartTimeUtc;
        if (_initialAge < TimeSpan.Zero)
            _initialAge = TimeSpan.Zero;

        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [TelemetryLayerTags.Schema] = layer.Schema,
            [TelemetryLayerTags.ServiceName] = layer.ServiceName,
            [TelemetryLayerTags.InvocationId] = context.InvocationId,
            [TelemetryTags.EventName] = _operationName,
            [TelemetryTags.DataBoundary] = context.EffectiveDataBoundary,
        };
        AddIfPresent(values, TelemetryTags.CommandTraceId, context.CommandTraceId);
        AddIfPresent(values, TelemetryTags.CommandSpanId, context.CommandSpanId);
        AddIfPresent(values, TelemetryTags.Command, context.Command);
        AddIfPresent(values, TelemetryTags.TenantId, context.TenantId);
        AddIfPresent(values, TelemetryTags.SubscriptionId, context.SubscriptionId);
        _metadata = FreezeTags(layer, values);

        _initialTags = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in RequiredMetadataKeys)
        {
            if (activity.GetTagItem(key) is { } value && layer.PropertyFilter.IsAllowed(key))
                _initialTags[key] = JsonSerializer.SerializeToElement(value);
        }
        foreach (var key in RootIdentityKeys)
        {
            if (_initialTags.TryGetValue(key, out var value))
                _metadata[key] = value;
        }
        ApplyMetadata(_initialTags);
        _resource = JsonSerializer.SerializeToElement(new Dictionary<string, object>
        {
            [TelemetryLayerTags.ServiceName] = layer.ServiceName,
            ["service.version"] = activity.Source.Version ?? "unknown",
        });

        foreach (var key in InvocationMetadataKeys)
            activity.SetTag(key, null);
        foreach (var (key, value) in values)
            activity.SetTag(key, value);
    }

    internal TraceEntry CaptureNormal(
        Activity activity,
        IReadOnlyDictionary<string, JsonElement>? terminalTags = null,
        ActivityStatusCode? terminalStatus = null)
    {
        var tags = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in activity.TagObjects)
        {
            if (_layer.PropertyFilter.IsAllowed(key))
                tags[key] = JsonSerializer.SerializeToElement(value);
        }
        Overlay(tags, terminalTags);
        ApplyMetadata(tags);

        var events = activity.Events.Select(evt => new
        {
            evt.Name,
            Timestamp = evt.Timestamp.ToString("O", CultureInfo.InvariantCulture),
            Attributes = evt.Tags.Where(tag => _layer.PropertyFilter.IsAllowed(tag.Key))
                .Select(tag => new KeyValuePair<string, JsonElement>(
                    tag.Key, JsonSerializer.SerializeToElement(tag.Value))).ToArray(),
        }).ToArray();
        return CreateEntry(activity.Duration.TotalMilliseconds,
            terminalStatus ?? activity.Status, tags, JsonSerializer.SerializeToElement(events),
            terminalStatus.HasValue ? null : activity.StatusDescription);
    }

    internal TraceEntry CaptureAtDeadline(
        TimeProvider timeProvider,
        long cutoff,
        IReadOnlyDictionary<string, JsonElement>? terminalTags,
        ActivityStatusCode? terminalStatus,
        bool root,
        int pendingProducers,
        int abandonedSpans)
    {
        var tags = new Dictionary<string, JsonElement>(_initialTags, StringComparer.OrdinalIgnoreCase);
        Overlay(tags, terminalTags);
        ApplyMetadata(tags);
        tags[TelemetryLayerTags.Completion] = JsonSerializer.SerializeToElement(root ? "deadline" : "abandoned");
        if (root)
        {
            tags[TelemetryLayerTags.PendingProducers] = JsonSerializer.SerializeToElement(pendingProducers);
            tags[TelemetryLayerTags.AbandonedSpans] = JsonSerializer.SerializeToElement(abandonedSpans);
        }
        return CreateEntry(
            Math.Max(0, (_initialAge + timeProvider.GetElapsedTime(_createdTimestamp, cutoff)).TotalMilliseconds),
            root ? terminalStatus ?? ActivityStatusCode.Unset : ActivityStatusCode.Error,
            tags, JsonSerializer.SerializeToElement(Array.Empty<object>()));
    }

    internal TraceEntry CaptureCompletedRoot(
        TraceEntry? normal,
        TimeProvider timeProvider,
        long cutoff,
        IReadOnlyDictionary<string, JsonElement>? terminalTags,
        ActivityStatusCode? terminalStatus)
    {
        if (normal is not null)
            return normal;
        var tags = new Dictionary<string, JsonElement>(_initialTags, StringComparer.OrdinalIgnoreCase);
        Overlay(tags, terminalTags);
        ApplyMetadata(tags);
        return CreateEntry(
            Math.Max(0, (_initialAge + timeProvider.GetElapsedTime(_createdTimestamp, cutoff)).TotalMilliseconds),
            terminalStatus ?? ActivityStatusCode.Unset, tags, JsonSerializer.SerializeToElement(Array.Empty<object>()));
    }

    internal static Dictionary<string, JsonElement> FreezeTags(
        TelemetryLayer layer, IEnumerable<KeyValuePair<string, object?>>? values)
    {
        var frozen = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (values is not null)
        {
            foreach (var (key, value) in values)
            {
                if (layer.PropertyFilter.IsAllowed(key))
                    frozen[key] = JsonSerializer.SerializeToElement(value);
            }
        }
        return frozen;
    }

    private TraceEntry CreateEntry(
        double duration, ActivityStatusCode status, Dictionary<string, JsonElement> tags,
        JsonElement events, string? statusDescription = null) =>
        new()
        {
            TraceId = _traceId,
            SpanId = _spanId,
            ParentSpanId = _parentSpanId,
            OperationName = _operationName,
            DisplayName = _displayName,
            StartTime = _startTime,
            Duration = Math.Max(0, duration),
            Status = status.ToString(),
            StatusDescription = statusDescription,
            Kind = _kind,
            TenantId = _context.TenantId,
            DataBoundary = _context.EffectiveDataBoundary,
            Tags = JsonSerializer.SerializeToElement(tags.ToArray()),
            Events = events,
            Resource = _resource,
        };

    private static void Overlay(
        Dictionary<string, JsonElement> target, IReadOnlyDictionary<string, JsonElement>? source)
    {
        if (source is null)
            return;
        foreach (var (key, value) in source)
            target[key] = value;
    }

    private void ApplyMetadata(Dictionary<string, JsonElement> tags)
    {
        foreach (var key in InvocationMetadataKeys)
            tags.Remove(key);
        foreach (var (key, value) in _metadata)
        {
            // Dictionary replacement otherwise keeps a caller's original key casing.
            tags.Remove(key);
            tags.Add(key, value);
        }
    }

    private static void AddIfPresent(Dictionary<string, object?> tags, string key, string? value)
    {
        if (value is not null)
            tags[key] = value;
    }
}
