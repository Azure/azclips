// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Core.Abstractions;
using OpenTelemetry;

namespace Core.Telemetry;

/// <summary>
/// Custom OpenTelemetry exporter that writes trace data synchronously to a local file.
/// </summary>
internal class FileExporter : BaseExporter<Activity>
{
    private static readonly DefaultTelemetryPropertyFilter AllowedTags = new();
    private readonly string _rootDirectory;
    private readonly bool _keepAllData;
    private string? _filePath;
    private readonly object _lock = new();

    /// <param name="rootDirectory">The folder trace files are written to.</param>
    /// <param name="keepAllData">
    /// Writes every tag, event attribute and the status description. Only for the local-only
    /// trace folder, which nothing but a local-collector uploader reads.
    /// </param>
    public FileExporter(string rootDirectory, bool keepAllData = false)
    {
        _rootDirectory = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
        _keepAllData = keepAllData;
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        try
        {
            var traces = new List<object>();

            foreach (var activity in batch)
            {
                _filePath ??= CreateTraceFilePath(activity.TraceId.ToString());

                // HTTP activities bypass TelemetryService and do not inherit invocation tags.
                // Persist the safe ancestor scope now because replay reconstructs parent IDs, not
                // live parent Activity objects that AriaActivityExporter could traverse later.
                var tags = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                AllowedTags.MergeAllowedTags(activity, tags);
                if (_keepAllData)
                {
                    foreach (var tag in activity.TagObjects)
                        tags[tag.Key] = tag.Value;
                }
                var dataBoundary = DataBoundaryRouting.ResolveUploadRegion(activity).ToDataBoundary();
                tags[TelemetryTags.DataBoundary] = dataBoundary;

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
                    dataBoundary,
                    entraObjectId = ResolveTag(activity, TelemetryTags.EntraObjectId),
                    tags = tags.ToArray(),
                    events = GetEvents(activity, _keepAllData),
                    resource = GetResource(activity)
                };

                traces.Add(_keepAllData ? WithStatusDescription(trace, activity.StatusDescription) : trace);
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
    /// Resolves persisted metadata from the Activity's parent chain.
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

    private static List<object> GetEvents(Activity activity, bool keepAllData)
    {
        var events = new List<object>();
        foreach (var evt in activity.Events)
        {
            events.Add(new
            {
                Name = evt.Name,
                Timestamp = evt.Timestamp.ToString("O"),
                Attributes = keepAllData
                    ? evt.Tags.ToArray()
                    : evt.Tags
                        .Select(tag => tag.Key == "exception.type"
                            ? new KeyValuePair<string, object?>(TelemetryTags.ExceptionType, tag.Value)
                            : tag)
                        .Where(tag => AllowedTags.IsAllowed(tag.Key))
                        .ToArray()
            });
        }
        return events;
    }

    // Status text is free-form error detail, so only the local-only folder carries it.
    private static object WithStatusDescription(object trace, string? statusDescription)
    {
        if (statusDescription is null)
            return trace;

        var node = JsonSerializer.SerializeToNode(trace)!.AsObject();
        node["statusDescription"] = statusDescription;
        return node;
    }

    private static Dictionary<string, object> GetResource(Activity activity)
    {
        var resource = new Dictionary<string, object>();

        for (var current = activity; current is not null; current = current.Parent)
        {
            if (current.Source.Name == "System.Net.Http" ||
                current.GetTagItem(TelemetryTags.ProductName) is not string productName ||
                string.IsNullOrEmpty(productName))
            {
                continue;
            }

            resource["service.name"] = productName;
            resource["service.version"] =
                current.GetTagItem(TelemetryTags.ProductVersion) as string
                ?? current.Source.Version
                ?? "unknown";
            return resource;
        }

        resource["service.name"] = activity.Source.Name;
        resource["service.version"] = activity.Source.Version ?? "unknown";
        return resource;
    }
}
