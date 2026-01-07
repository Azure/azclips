using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using OpenTelemetry;

namespace Core.Telemetry;

/// <summary>
/// Custom OpenTelemetry exporter that writes trace data synchronously to a local file.
/// </summary>
internal class FileExporter : BaseExporter<Activity>
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public FileExporter(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        try
        {
            var traces = new List<object>();
            
            foreach (var activity in batch)
            {
                var trace = new
                {
                    TraceId = activity.TraceId.ToString(),
                    SpanId = activity.SpanId.ToString(),
                    ParentSpanId = activity.ParentSpanId.ToString(),
                    OperationName = activity.OperationName,
                    DisplayName = activity.DisplayName,
                    StartTime = activity.StartTimeUtc.ToString("O"),
                    Duration = activity.Duration.TotalMilliseconds,
                    Status = activity.Status.ToString(),
                    StatusDescription = activity.StatusDescription,
                    Kind = activity.Kind.ToString(),
                    Tags = activity.TagObjects,
                    Events = GetEvents(activity),
                    Resource = GetResource(activity)
                };
                
                traces.Add(trace);
            }

            // Synchronously write to file
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(traces, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                // Append to file with timestamp
                File.AppendAllText(_filePath, $"\n--- Batch at {DateTime.UtcNow:O} ---\n{json}\n");
            }

            return ExportResult.Success;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error exporting traces to file: {ex.Message}");
            return ExportResult.Failure;
        }
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
