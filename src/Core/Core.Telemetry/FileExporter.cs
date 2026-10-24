// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry;

namespace Core.Telemetry;

/// <summary>
/// Custom OpenTelemetry exporter that writes trace data synchronously to a local file.
/// </summary>
internal class FileExporter : BaseExporter<Activity>
{
    private static readonly ITelemetryPropertyFilter AllowedTags = new DefaultTelemetryPropertyFilter();
    private readonly string _rootDirectory;
    private string? _filePath;
    private readonly object _lock = new();

    public FileExporter(string rootDirectory)
    {
        _rootDirectory = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        try
        {
            var traces = new List<object>();

            foreach (var activity in batch)
            {
                _filePath ??= CreateTraceFilePath(activity.TraceId.ToString());

                // Resolved per activity by walking its own parent chain to the tagged root
                // (see TelemetryTags.TenantId/DataBoundary/EntraObjectId) rather than from ambient async-local
                // state: the tag is a mutation on a shared Activity object, so it stays correct
                // even when resolved from a different async flow than the one exporting here.
                var trace = new
                {
                    traceId = activity.TraceId.ToString(),
                    spanId = activity.SpanId.ToString(),
                    parentSpanId = activity.ParentSpanId.ToString(),
                    operationName = activity.OperationName,
                    startTime = activity.StartTimeUtc.ToString("O"),
                    durationMs = activity.Duration.TotalMilliseconds,
                    status = activity.Status.ToString(),
                    kind = activity.Kind.ToString(),
                    tenantId = ResolveTag(activity, TelemetryTags.TenantId),
                    dataBoundary = ResolveTag(activity, TelemetryTags.DataBoundary),
                    entraObjectId = ResolveTag(activity, TelemetryTags.EntraObjectId),
                    tags = activity.TagObjects
                        .Where(tag => AllowedTags.IsAllowed(tag.Key))
                        .ToArray(),
                    events = GetEvents(activity),
                    resource = GetResource(activity)
                };

                traces.Add(trace);
            }

            // An empty batch (unlikely) leaves nothing to write, and the TraceId that names the
            // file is still unknown. Wait for the next one.
            if (traces.Count == 0)
            {
                return ExportResult.Success;
            }

            // Synchronously write to file
            lock (_lock)
            {
                File.AppendAllText(_filePath!, TraceFileFormat.FormatBatch(traces, DateTime.UtcNow));
            }

            return ExportResult.Success;
        }
        catch (Exception ex)
        {
            // Raw Console.Error: Core.Telemetry sits below Core.Runtime; routing through
            // ILoggingService would invert the layer stack and risk recursion if any future
            // logger path opens a telemetry activity. Matches OpenTelemetry SelfLog / Serilog SelfLog.
            Console.Error.WriteLine($"Error exporting traces to file: {ex.Message}");
            return ExportResult.Failure;
        }
    }

    /// <summary>Resolves the file a trace buffers into, creating its directory if needed.</summary>
    private string CreateTraceFilePath(string traceId)
    {
        var path = Path.Combine(_rootDirectory, TraceFileStore.FileNameFor(traceId));

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return path;
    }

    /// <summary>
    /// Walks from <paramref name="activity"/> up through its parents looking for
    /// <paramref name="tagName"/> \u2014 set once on the root by whichever flow resolved it.
    /// </summary>
    private static string? ResolveTag(Activity activity, string tagName)
    {
        for (var current = activity; current is not null; current = current.Parent)
        {
            if (current.GetTagItem(tagName) is string value && !string.IsNullOrEmpty(value))
                return value;
        }
        return null;
    }

    private static List<object> GetEvents(Activity activity)
    {
        var events = new List<object>();
        foreach (var evt in activity.Events)
        {
            events.Add(new
            {
                Name = evt.Name,
                Timestamp = evt.Timestamp.ToString("O"),
                Attributes = evt.Tags
                    .Where(tag => AllowedTags.IsAllowed(tag.Key))
                    .ToArray()
            });
        }
        return events;
    }

    private static Dictionary<string, object> GetResource(Activity activity)
    {
        var resource = new Dictionary<string, object>();
        
        if (activity.Source != null)
        {
            resource["service.name"] = activity.Source.Name;
            resource["service.version"] = activity.Source.Version ?? "unknown";
        }

        return resource;
    }
}
