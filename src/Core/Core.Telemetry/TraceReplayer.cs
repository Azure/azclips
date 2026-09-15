// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using SpanKey = (string? TraceId, string? SpanId);

namespace Core.Telemetry;

/// <summary>
/// Replays trace entries through an <see cref="ActivitySource"/>, preserving the original
/// TraceId and source-recorded hierarchy tags. Exporter parent IDs can only be remapped
/// within a batch; they are not the identity of a source-recorded span.
/// </summary>
internal static class TraceReplayer
{
    /// <summary>
    /// Replays entries parent-before-child, remapping each child's parent context to the
    /// SpanId its parent was assigned during replay.
    /// </summary>
    internal static void ReplayEntries(ActivitySource source, IEnumerable<TraceEntry> entries)
    {
        var entriesList = entries.ToList();
        if (entriesList.Count == 0)
            return;

        var ordered = TopologicallyOrder(entriesList);

        // Span identity is scoped by TraceId, even when a batch contains several traces.
        var spanIdMap = new Dictionary<SpanKey, ActivitySpanId>();

        foreach (var entry in ordered)
        {
            ReplayTrace(source, entry, spanIdMap);
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
                bySpanId[(e.TraceId, e.SpanId)] = e;
        }

        var childrenOf = new Dictionary<SpanKey, List<TraceEntry>>();
        var roots = new List<TraceEntry>();

        foreach (var e in entries)
        {
            var parentKey = (e.TraceId, e.ParentSpanId);
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
        var queue = new Queue<TraceEntry>(roots);

        while (queue.Count > 0)
        {
            var entry = queue.Dequeue();
            var key = (entry.TraceId, entry.SpanId);

            if (!string.IsNullOrEmpty(entry.SpanId) && !visited.Add(key))
                continue;

            ordered.Add(entry);

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
                var key = (e.TraceId, e.SpanId);
                if (!string.IsNullOrEmpty(e.SpanId) && visited.Add(key))
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
    private static void ReplayTrace(ActivitySource source, TraceEntry entry, Dictionary<SpanKey, ActivitySpanId> spanIdMap)
    {
        if (string.IsNullOrEmpty(entry.TraceId))
            return;

        var kind = Enum.TryParse<ActivityKind>(entry.Kind, out var k) ? k : ActivityKind.Internal;

        // Use the parent's new SpanId when the parent was replayed in this batch; otherwise a random one,
        // since an all-zero SpanId is invalid and would lose the TraceId.
        ActivitySpanId parentSpanId;
        if (!string.IsNullOrEmpty(entry.ParentSpanId)
            && entry.ParentSpanId != "0000000000000000"
            && spanIdMap.TryGetValue((entry.TraceId, entry.ParentSpanId), out var remapped))
        {
            parentSpanId = remapped;
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
            entry.OperationName ?? entry.DisplayName ?? "unknown",
            kind,
            parentContext,
            startTime: startTime);

        if (activity == null)
            return;
        activity.DisplayName = entry.DisplayName ?? activity.OperationName;

        // Recorded before disposal so descendants can reference it.
        if (!string.IsNullOrEmpty(entry.SpanId))
            spanIdMap[(entry.TraceId, entry.SpanId)] = activity.SpanId;

        // Restore source tags verbatim, never deriving hierarchy from replay IDs or time.
        if (entry.Tags.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in entry.Tags.EnumerateArray())
            {
                if (tag.TryGetProperty("Key", out var keyEl) &&
                    tag.TryGetProperty("Value", out var valueEl))
                {
                    var key = keyEl.GetString();
                    if (key != null)
                    {
                        activity.SetTag(key, RestoreTagValue(valueEl));
                    }
                }
            }
        }

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

        // Restore status
        if (Enum.TryParse<ActivityStatusCode>(entry.Status, out var status))
            activity.SetStatus(status, entry.StatusDescription);

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
