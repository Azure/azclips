// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Core.Abstractions;
using SpanKey = (string? TraceId, string? SpanId, Core.Abstractions.UploadRegion UploadRegion);

namespace Core.Telemetry;

internal sealed class TraceReplayContext
{
    private readonly Dictionary<TelemetryTarget,
        Dictionary<(string? TraceId, string? SpanId, UploadRegion UploadRegion), ActivitySpanId>> _spanIds = [];

    internal Dictionary<(string? TraceId, string? SpanId, UploadRegion UploadRegion), ActivitySpanId> ForTarget(
        TelemetryTarget target)
    {
        if (!_spanIds.TryGetValue(target, out var spanIds))
            _spanIds[target] = spanIds = [];
        return spanIds;
    }
}

/// <summary>
/// Replays trace entries through an <see cref="ActivitySource"/>, preserving the original
/// TraceId and native parent relationships within the replay batch.
/// </summary>
internal static class TraceReplayer
{
    /// <summary>
    /// Replays entries parent-before-child, remapping each child's parent context to the
    /// SpanId its parent was assigned during replay.
    /// </summary>
    internal static void ReplayEntries(
        ActivitySource source,
        IEnumerable<TraceEntry> entries,
        TraceReplayProfile profile = TraceReplayProfile.Legacy,
        Dictionary<SpanKey, ActivitySpanId>? sharedSpanIdMap = null)
    {
        var entriesList = entries.ToList();
        if (entriesList.Count == 0)
            return;

        var ordered = TopologicallyOrder(entriesList);

        // Keep span mappings isolated by trace and upload region.
        var spanIdMap = sharedSpanIdMap ?? new Dictionary<SpanKey, ActivitySpanId>();

        var executionContext = ExecutionContext.Capture();
        if (executionContext is not null)
        {
            ExecutionContext.Run(executionContext, _ => ReplayIsolated(), null);
        }
        else
        {
            var previous = Activity.Current;
            try
            {
                ReplayIsolated();
            }
            finally
            {
                Activity.Current = previous;
            }
        }

        void ReplayIsolated()
        {
            // A zero parent span otherwise borrows Activity.Current even with an explicit TraceId.
            Activity.Current = null;
            foreach (var entry in ordered)
                ReplayTrace(source, entry, spanIdMap, profile);
        }
    }

    /// <summary>
    /// Orders entries so every parent precedes its children. Unparented entries are roots;
    /// entries left over from cycles are appended at the end.
    /// </summary>
    private static List<TraceEntry> TopologicallyOrder(List<TraceEntry> entries)
    {
        var bySpanId = new Dictionary<SpanKey, TraceEntry>();
        foreach (var e in entries)
        {
            if (!string.IsNullOrEmpty(e.SpanId))
                bySpanId[(e.TraceId, e.SpanId, e.GetUploadRegion())] = e;
        }

        var childrenOf = new Dictionary<SpanKey, List<TraceEntry>>();
        var roots = new List<TraceEntry>();

        foreach (var e in entries)
        {
            var parentKey = (e.TraceId, e.ParentSpanId, e.GetUploadRegion());
            if (string.IsNullOrEmpty(e.ParentSpanId)
                || e.ParentSpanId == "0000000000000000"
                || !bySpanId.ContainsKey(parentKey))
            {
                roots.Add(e);
            }
            else
            {
                if (!childrenOf.TryGetValue(parentKey, out var list))
                {
                    list = new List<TraceEntry>();
                    childrenOf[parentKey] = list;
                }
                list.Add(e);
            }
        }

        var ordered = new List<TraceEntry>(entries.Count);
        var visited = new HashSet<SpanKey>();
        // Tracks what the walk emitted. visited cannot serve for this: an entry with no SpanId is
        // emitted but deliberately kept out of visited, because every such entry would otherwise
        // collide on the same (traceId, "") key and only the first would survive.
        var emitted = new HashSet<TraceEntry>();
        var queue = new Queue<TraceEntry>(roots);

        while (queue.Count > 0)
        {
            var entry = queue.Dequeue();
            var key = (entry.TraceId, entry.SpanId, entry.GetUploadRegion());

            if (!string.IsNullOrEmpty(entry.SpanId) && !visited.Add(key))
                continue;

            ordered.Add(entry);
            emitted.Add(entry);

            if (!string.IsNullOrEmpty(entry.SpanId) && childrenOf.TryGetValue(key, out var children))
            {
                foreach (var child in children)
                    queue.Enqueue(child);
            }
        }

        // Entries left out by cycles, appended as best-effort roots.
        {
            foreach (var e in entries)
            {
                if (emitted.Contains(e))
                    continue;

                var key = (e.TraceId, e.SpanId, e.GetUploadRegion());
                // Mirrors the walk above, which admits an empty SpanId and deduplicates only real
                // ones. TraceEntry allows null ids, and an entry with no SpanId whose parent sits in
                // a cycle is never reached by the walk, so requiring one here dropped it silently.
                if (string.IsNullOrEmpty(e.SpanId) || visited.Add(key))
                {
                    ordered.Add(e);
                }
            }
        }

        return ordered;
    }

    /// <summary>
    /// Reconstructs one Activity from an entry and records its new SpanId in
    /// <paramref name="spanIdMap"/> for its descendants.
    /// </summary>
    private static void ReplayTrace(
        ActivitySource source,
        TraceEntry entry,
        Dictionary<SpanKey, ActivitySpanId> spanIdMap,
        TraceReplayProfile profile)
    {
        if (string.IsNullOrEmpty(entry.TraceId))
            return;

        var kind = Enum.TryParse<ActivityKind>(entry.Kind, out var k) ? k : ActivityKind.Internal;

        ActivitySpanId parentSpanId;
        if (string.IsNullOrEmpty(entry.ParentSpanId) || entry.ParentSpanId == "0000000000000000")
        {
            parentSpanId = default;
        }
        else if (spanIdMap.TryGetValue(
                     (entry.TraceId, entry.ParentSpanId, entry.GetUploadRegion()),
                     out var remapped))
        {
            parentSpanId = remapped;
        }
        else if (profile == TraceReplayProfile.ManagedLayer)
        {
            parentSpanId = ActivitySpanId.CreateFromString(entry.ParentSpanId.AsSpan());
        }
        else
        {
            parentSpanId = ActivitySpanId.CreateRandom();
        }

        var parentContext = new ActivityContext(
            ActivityTraceId.CreateFromString(entry.TraceId.AsSpan()),
            parentSpanId,
            ActivityTraceFlags.Recorded);

        var startTime = DateTimeOffset.TryParse(
            entry.StartTime,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsedStart)
            ? parsedStart
            : DateTimeOffset.UtcNow;

        using var activity = source.StartActivity(
            entry.OperationName ?? "unknown",
            kind,
            parentContext,
            startTime: startTime);

        if (activity == null)
            return;

        if (!string.IsNullOrWhiteSpace(entry.EntraObjectId))
        {
            activity.SetTag(TelemetryTags.EntraObjectId, entry.EntraObjectId);
        }

        // Recorded before disposal so descendants can reference it.
        if (!string.IsNullOrEmpty(entry.SpanId))
            spanIdMap[(entry.TraceId, entry.SpanId, entry.GetUploadRegion())] =
                activity.SpanId;

        if (entry.Tags.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in entry.Tags.EnumerateArray())
            {
                if (tag.TryGetProperty("Key", out var keyEl) &&
                    tag.TryGetProperty("Value", out var valueEl))
                {
                    var key = keyEl.GetString();
                    if (key != null && key != TelemetryTags.DataBoundary && key != TelemetryTags.DataBoundaryOutcome)
                    {
                        activity.SetTag(key, profile == TraceReplayProfile.ManagedLayer
                            ? RestoreTagValue(valueEl)
                            : valueEl.ValueKind == JsonValueKind.String ? valueEl.GetString() : valueEl.ToString());
                    }
                }
            }
        }

        activity.SetTag(TelemetryTags.UploadRegion, entry.GetUploadRegion().ToString());

        // Restore events
        if (entry.Events.ValueKind == JsonValueKind.Array)
        {
            foreach (var evt in entry.Events.EnumerateArray())
            {
                var name = evt.TryGetProperty("Name", out var nameEl) ? nameEl.GetString() : null;
                if (name == null) continue;

                var timestamp = evt.TryGetProperty("Timestamp", out var tsEl) && tsEl.GetString() is string tsStr
                    && DateTimeOffset.TryParse(tsStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                    ? parsed
                    : DateTimeOffset.UtcNow;

                var tags = new ActivityTagsCollection();
                if (evt.TryGetProperty("Attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var attr in attrs.EnumerateArray())
                    {
                        if (attr.TryGetProperty("Key", out var ak) &&
                            attr.TryGetProperty("Value", out var av))
                        {
                            var attrKey = ak.GetString();
                            if (attrKey != null)
                                tags[attrKey] = av.ValueKind == JsonValueKind.String ? av.GetString() : av.ToString();
                        }
                    }
                }

                activity.AddEvent(new ActivityEvent(name, timestamp, tags));
            }
        }

        // Restore status without free-form descriptions, which can contain sensitive error text.
        if (Enum.TryParse<ActivityStatusCode>(entry.Status, out var status))
            activity.SetStatus(status);

        // Restore end time (start + duration)
        activity.SetEndTime(activity.StartTimeUtc + TimeSpan.FromMilliseconds(entry.Duration));
    }

    private static object? RestoreTagValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetInt32(out var number) => number,
        JsonValueKind.Number when value.TryGetInt64(out var number) => number,
        JsonValueKind.Number => value.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => value.ToString(),
    };
}
