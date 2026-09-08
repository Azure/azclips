// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Core.Telemetry;

/// <summary>
/// Replays trace entries through an <see cref="ActivitySource"/>, preserving the original
/// TraceId and parent/child hierarchy.
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

        // Original SpanId -> the new SpanId assigned during replay.
        var spanIdMap = new Dictionary<string, ActivitySpanId>(StringComparer.Ordinal);

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
        var bySpanId = new Dictionary<string, TraceEntry>(StringComparer.Ordinal);
        foreach (var e in entries)
        {
            if (!string.IsNullOrEmpty(e.SpanId))
                bySpanId[e.SpanId] = e;
        }

        var childrenOf = new Dictionary<string, List<TraceEntry>>(StringComparer.Ordinal);
        var roots = new List<TraceEntry>();

        foreach (var e in entries)
        {
            var parentKey = e.ParentSpanId;
            if (string.IsNullOrEmpty(parentKey)
                || parentKey == "0000000000000000"
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
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<TraceEntry>(roots);

        while (queue.Count > 0)
        {
            var entry = queue.Dequeue();
            var key = entry.SpanId ?? string.Empty;

            if (!string.IsNullOrEmpty(key) && !visited.Add(key))
                continue;

            ordered.Add(entry);

            if (!string.IsNullOrEmpty(key) && childrenOf.TryGetValue(key, out var children))
            {
                foreach (var child in children)
                    queue.Enqueue(child);
            }
        }

        // Entries left out by cycles, appended as best-effort roots.
        {
            foreach (var e in entries)
            {
                var key = e.SpanId ?? string.Empty;
                if (string.IsNullOrEmpty(key) || !visited.Contains(key))
                {
                    ordered.Add(e);
                    if (!string.IsNullOrEmpty(key))
                        visited.Add(key);
                }
            }
        }

        return ordered;
    }

    /// <summary>
    /// Reconstructs one Activity from an entry and records its new SpanId in
    /// <paramref name="spanIdMap"/> for its descendants.
    /// </summary>
    private static void ReplayTrace(ActivitySource source, TraceEntry entry, Dictionary<string, ActivitySpanId> spanIdMap)
    {
        if (string.IsNullOrEmpty(entry.TraceId))
            return;

        var kind = Enum.TryParse<ActivityKind>(entry.Kind, out var k) ? k : ActivityKind.Internal;

        // Use the parent's new SpanId when the parent was replayed in this batch; otherwise a random one,
        // since an all-zero SpanId is invalid and would lose the TraceId.
        ActivitySpanId parentSpanId;
        if (!string.IsNullOrEmpty(entry.ParentSpanId)
            && entry.ParentSpanId != "0000000000000000"
            && spanIdMap.TryGetValue(entry.ParentSpanId, out var remapped))
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
            entry.DisplayName ?? entry.OperationName ?? "unknown",
            kind,
            parentContext,
            startTime: startTime);

        if (activity == null)
            return;

        // Recorded before disposal so descendants can reference it.
        if (!string.IsNullOrEmpty(entry.SpanId))
            spanIdMap[entry.SpanId] = activity.SpanId;

        // Restore tags
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
                        activity.SetTag(key, valueEl.ValueKind == JsonValueKind.String
                            ? valueEl.GetString()
                            : valueEl.ToString());
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
}
