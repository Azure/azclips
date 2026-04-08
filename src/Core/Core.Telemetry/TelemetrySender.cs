using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// Reads locally buffered trace files and uploads them to Azure Monitor.
/// Designed to run in a background process spawned after the CLI command completes,
/// so that telemetry upload latency does not affect CLI responsiveness.
/// </summary>
public static class TelemetrySender
{
    /// <summary>
    /// Sends all pending trace files to Azure Monitor, then deletes them.
    /// </summary>
    public static Task SendPendingAsync()
    {
        var traceDir = TelemetryService.GetTraceDirectory();
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

        using var source = new ActivitySource(
            TelemetryService.DefaultServiceName,
            TelemetryService.DefaultServiceVersion);

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(TelemetryService.DefaultServiceName)
            .ConfigureResource(r => r.AddService(
                serviceName: TelemetryService.DefaultServiceName,
                serviceVersion: TelemetryService.DefaultServiceVersion))
            .AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString = TelemetryService.AzureMonitorConnectionString;
            })
            .Build()!;

        var replayedFiles = new List<string>();

        foreach (var file in claimedFiles)
        {
            try
            {
                var content = File.ReadAllText(file);
                var traces = ParseTraceFile(content);

                foreach (var trace in traces)
                {
                    ReplayTrace(source, trace);
                }

                replayedFiles.Add(file);
            }
            catch
            {
                // Best-effort: skip files that fail to parse.
                // They stay as .uploading and will be reclaimed after the stale timeout.
            }
        }

        // Only delete files after the exporter has successfully flushed.
        // If flush fails or times out, files remain as .uploading and will be
        // reclaimed by the stale-file recovery on the next invocation.
        if (provider.ForceFlush(timeoutMilliseconds: 10_000))
        {
            foreach (var file in replayedFiles)
            {
                try { File.Delete(file); } catch { }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Parses a trace file containing multiple JSON batches separated by headers.
    /// File format: "--- Batch at {timestamp} ---\n[{traces}]\n"
    /// </summary>
    private static List<TraceEntry> ParseTraceFile(string content)
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
    /// Reconstructs an Activity from a deserialized trace entry and feeds it
    /// through the ActivitySource so the TracerProvider exports it.
    /// TraceId is preserved via parent context; SpanId will differ from the original.
    /// </summary>
    private static void ReplayTrace(ActivitySource source, TraceEntry entry)
    {
        if (string.IsNullOrEmpty(entry.TraceId))
            return;

        var kind = Enum.TryParse<ActivityKind>(entry.Kind, out var k) ? k : ActivityKind.Internal;

        // Use parent context to preserve the original TraceId
        var parentSpanId = string.IsNullOrEmpty(entry.ParentSpanId) || entry.ParentSpanId == "0000000000000000"
            ? default
            : ActivitySpanId.CreateFromString(entry.ParentSpanId.AsSpan());

        var parentContext = new ActivityContext(
            ActivityTraceId.CreateFromString(entry.TraceId.AsSpan()),
            parentSpanId,
            ActivityTraceFlags.Recorded);

        using var activity = source.StartActivity(
            entry.DisplayName ?? entry.OperationName ?? "unknown",
            kind,
            parentContext);

        if (activity == null)
            return;

        // Restore start time
        if (DateTime.TryParse(entry.StartTime, out var startTime))
            activity.SetStartTime(startTime.ToUniversalTime());

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
                    ? DateTimeOffset.Parse(tsStr)
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
    private class TraceEntry
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
