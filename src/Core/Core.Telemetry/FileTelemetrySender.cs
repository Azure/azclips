// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Azure.Monitor.OpenTelemetry.Exporter;
using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// Telemetry sender for CLI: buffers traces to local files via FileExporter,
/// then replays them to Azure Monitor in a background process.
/// Also used by CLI's --upload-telemetry spawned process.
/// </summary>
public class FileTelemetrySender : ITelemetrySender
{
    private const string FallbackSourceName = "AzclipsTelemetryUploader";
    private const string TracesSubdirectoryName = "traces";

    /// <summary>
    /// Gets the shared trace directory for the background uploader picks up all files from one location.
    /// Uses the same <c>{LocalApplicationData}/.azclips/</c> root as auth profiles for consistency.
    /// </summary>
    public static string GetTraceDirectory() =>
        Path.Combine(
            HomePaths.GetDefaultLocalAppDataRoot(),
            TracesSubdirectoryName);

    public void ConfigureExporter(TracerProviderBuilder builder, string traceDirectory)
    {
        builder.AddProcessor(new SimpleActivityExportProcessor(new FileExporter(traceDirectory)));
    }

    Task ITelemetrySender.SendPendingAsync() => SendPendingStatic();

    /// <summary>
    /// Sends all pending trace files to Azure Monitor, then deletes them.
    /// Static entry point for CLI's --upload-telemetry spawned process.
    /// </summary>
    public static Task SendPendingStatic()
    {
        var traceDir = GetTraceDirectory();
        if (!Directory.Exists(traceDir))
            return Task.CompletedTask;

        // Reclaim stale .uploading files from crashed uploaders (older than 5 minutes)
        foreach (var stale in Directory.GetFiles(traceDir, "traces-*.uploading"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(stale) < DateTime.UtcNow.AddMinutes(-5))
                    File.Move(stale, Path.ChangeExtension(stale, ".log"));
            }
            catch { }
        }

        var files = Directory.GetFiles(traceDir, "traces-*.log");
        if (files.Length == 0)
            return Task.CompletedTask;

        // Claim files atomically by renaming to .uploading before reading.
        // This prevents concurrent uploaders from processing the same file
        // and avoids reading partially-written files from in-flight CLI instances.
        var claimedFiles = new List<string>();
        foreach (var file in files)
        {
            var claimed = Path.ChangeExtension(file, ".uploading");
            try
            {
                File.Move(file, claimed);
                claimedFiles.Add(claimed);
            }
            catch
            {
                // Another uploader already claimed this file, or the writer still has it locked. Skip.
            }
        }

        if (claimedFiles.Count == 0)
            return Task.CompletedTask;

        // Group traces by original service name (CLI/PowerShell) so each group
        // is uploaded with the correct cloud_RoleName in App Insights.
        var tracesByService = new Dictionary<string, List<(string file, List<TraceEntry> traces)>>();

        foreach (var file in claimedFiles)
        {
            try
            {
                var content = File.ReadAllText(file);
                var traces = ParseTraceFile(content);
                var serviceName = ExtractServiceName(traces) ?? FallbackSourceName;

                if (!tracesByService.ContainsKey(serviceName))
                    tracesByService[serviceName] = new();
                tracesByService[serviceName].Add((file, traces));
            }
            catch
            {
                // Best-effort: skip files that fail to parse.
            }
        }

        var replayedFiles = new List<string>();

        foreach (var (serviceName, fileGroup) in tracesByService)
        {
            using var source = new ActivitySource(serviceName);
            using var provider = Sdk.CreateTracerProviderBuilder()
                .AddSource(serviceName)
                .ConfigureResource(r => r.AddService(serviceName: serviceName))
                .AddAzureMonitorTraceExporter(options =>
                {
                    options.ConnectionString = WellKnownEndpoints.AzureMonitorConnectionString;
                })
                .Build()!;

            // Flatten across all files in the group so the topological replay can
            // resolve parent/child links even when they live in different files.
            var allEntries = fileGroup.SelectMany(f => f.traces);
            ReplayEntries(source, allEntries);

            // Only mark files for deletion if flush succeeds within the timeout.
            // If flush fails/times out, files remain on disk for the next upload attempt.
            if (provider.ForceFlush(timeoutMilliseconds: 10_000))
            {
                foreach (var (file, _) in fileGroup)
                    replayedFiles.Add(file);
            }
        }

        foreach (var file in replayedFiles)
        {
            try { File.Delete(file); } catch { }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Extracts the original service name from the Resource field of the first trace entry.
    /// </summary>
    private static string? ExtractServiceName(List<TraceEntry> traces)
    {
        if (traces.Count == 0)
            return null;

        var trace = traces[0];
        if (trace.Resource.ValueKind == JsonValueKind.Object &&
            trace.Resource.TryGetProperty("service.name", out var nameEl) &&
            nameEl.GetString() is string name &&
            !string.IsNullOrEmpty(name))
        {
            return name;
        }
        return null;
    }

    /// <summary>
    /// Parses a trace file containing multiple JSON batches separated by headers.
    /// File format: "--- Batch at {timestamp} ---\n[{traces}]\n"
    /// </summary>
    internal static List<TraceEntry> ParseTraceFile(string content)
    {
        var allTraces = new List<TraceEntry>();
        var parts = content.Split("--- Batch at ");

        foreach (var part in parts)
        {
            var jsonStart = part.IndexOf('[');
            if (jsonStart < 0) continue;
            var jsonEnd = part.LastIndexOf(']');
            if (jsonEnd < 0) continue;

            var json = part[jsonStart..(jsonEnd + 1)];

            try
            {
                var traces = JsonSerializer.Deserialize<List<TraceEntry>>(json);
                if (traces != null)
                    allTraces.AddRange(traces);
            }
            catch
            {
                // Skip malformed batches
            }
        }

        return allTraces;
    }

    /// <summary>
    /// Replays a batch of trace entries through the given <see cref="ActivitySource"/>.
    /// Entries are first ordered so every parent is replayed before any of its children,
    /// then each child's parent context is rewritten to point at the parent's newly
    /// assigned SpanId. This preserves the original parent/child hierarchy in App
    /// Insights even though OpenTelemetry .NET does not let us override SpanId directly.
    /// </summary>
    internal static void ReplayEntries(ActivitySource source, IEnumerable<TraceEntry> entries)
    {
        var entriesList = entries.ToList();
        if (entriesList.Count == 0)
            return;

        var ordered = TopologicallyOrder(entriesList);

        // Maps each entry's original SpanId to the new SpanId assigned when its
        // replay activity was started. Children look up their parent here so the
        // exported telemetry shows a connected tree under the original TraceId.
        var spanIdMap = new Dictionary<string, ActivitySpanId>(StringComparer.Ordinal);

        foreach (var entry in ordered)
        {
            ReplayTrace(source, entry, spanIdMap);
        }
    }

    /// <summary>
    /// Orders entries so every parent precedes its children. Entries whose
    /// <see cref="TraceEntry.ParentSpanId"/> is empty, zero, or not present in
    /// the batch are treated as roots. Cycles (malformed input) are broken
    /// defensively and the remaining entries are appended at the end.
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

        // Sweep up any entries left out by cycles so we never silently drop telemetry —
        // they go on the end as best-effort roots.
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
    /// Reconstructs an Activity from a deserialized trace entry and feeds it
    /// through the ActivitySource so the TracerProvider exports it.
    /// The original TraceId is preserved via parent context. The original SpanId
    /// cannot be preserved (the OTel API generates one), so when the entry's
    /// parent is also being replayed we look up the parent's new SpanId in
    /// <paramref name="spanIdMap"/> and use that — keeping the hierarchy intact.
    /// After the activity is created we record its new SpanId in the map so
    /// any descendants in the same batch can reference it.
    /// </summary>
    private static void ReplayTrace(ActivitySource source, TraceEntry entry, Dictionary<string, ActivitySpanId> spanIdMap)
    {
        if (string.IsNullOrEmpty(entry.TraceId))
            return;

        var kind = Enum.TryParse<ActivityKind>(entry.Kind, out var k) ? k : ActivityKind.Internal;

        // Resolve the parent SpanId:
        //   1. If the entry's original parent has already been replayed in this batch,
        //      use the *new* SpanId we assigned to that parent so the hierarchy is preserved.
        //   2. Otherwise (root, or parent is out of batch / unknown), use a random SpanId so
        //      the W3C context stays valid (an all-zero SpanId is considered invalid and
        //      StartActivity may ignore it, losing the original TraceId).
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

        // Parse start time before creating the activity so we can pass it to StartActivity directly.
        var startTime = DateTime.TryParse(entry.StartTime, out var st)
            ? new DateTimeOffset(st.ToUniversalTime())
            : DateTimeOffset.UtcNow;

        using var activity = source.StartActivity(
            entry.DisplayName ?? entry.OperationName ?? "unknown",
            kind,
            parentContext,
            startTime: startTime);

        if (activity == null)
            return;

        // Record the new SpanId before the activity is disposed so descendants
        // processed later in the same batch can wire themselves up to it.
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

    /// <summary>
    /// Deserialization model matching the JSON format written by FileExporter.
    /// </summary>
    internal class TraceEntry
    {
        public string? TraceId { get; set; }
        public string? SpanId { get; set; }
        public string? ParentSpanId { get; set; }
        public string? OperationName { get; set; }
        public string? DisplayName { get; set; }
        public string? StartTime { get; set; }
        public double Duration { get; set; }
        public string? Status { get; set; }
        public string? StatusDescription { get; set; }
        public string? Kind { get; set; }
        public JsonElement Tags { get; set; }
        public JsonElement Events { get; set; }
        public JsonElement Resource { get; set; }
    }
}
