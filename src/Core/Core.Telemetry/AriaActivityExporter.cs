// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter.OneCollector;
using OpenTelemetry.Logs;

namespace Core.Telemetry;

/// <summary>
/// Bridges OpenTelemetry Activity (trace) spans to Aria via the OneCollector log exporter.
/// Each completed Activity is converted to a structured log record that OneCollector
/// serializes and sends to the 1DS/Aria ingestion endpoint.
/// </summary>
internal sealed class AriaActivityExporter : BaseExporter<Activity>
{
    private static readonly EventId CommandEvent = new(1, "CommandExecution");
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    public AriaActivityExporter(string connectionString, Uri? ingestionEndpoint = null)
    {
        _loggerFactory = LoggerFactory.Create(logging =>
        {
            logging.AddOpenTelemetry(otel =>
            {
                otel.IncludeScopes = true;
                otel.AddOneCollectorExporter(connectionString, builder =>
                {
                    if (ingestionEndpoint is not null)
                    {
                        builder.ConfigureTransportOptions(
                            transport => transport.Endpoint = ingestionEndpoint);
                    }
                });
            });
        });
        _logger = _loggerFactory.CreateLogger("Azclips");
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (var activity in batch)
        {
            // Skip HTTP instrumentation spans — only export application-level spans.
            if (activity.Source.Name == "System.Net.Http")
                continue;

            using (_logger.BeginScope(BuildScope(activity)))
            {
                if (activity.Status == ActivityStatusCode.Error)
                {
                    _logger.LogError(CommandEvent, "{OperationName}", activity.OperationName);
                }
                else
                {
                    _logger.LogInformation(CommandEvent, "{OperationName}", activity.OperationName);
                }
            }
        }

        return ExportResult.Success;
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        _loggerFactory.Dispose();
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _loggerFactory.Dispose();
        base.Dispose(disposing);
    }

    private static Dictionary<string, object?> BuildScope(Activity activity)
    {
        var scope = new Dictionary<string, object?>
        {
            ["TraceId"] = activity.TraceId.ToString(),
            ["SpanId"] = activity.SpanId.ToString(),
            ["DurationMs"] = activity.Duration.TotalMilliseconds,
            ["StartTime"] = activity.StartTimeUtc.ToString("o"),
            ["Status"] = activity.Status.ToString(),
            ["Kind"] = activity.Kind.ToString(),
        };

        if (!string.IsNullOrEmpty(activity.StatusDescription))
            scope["StatusDescription"] = activity.StatusDescription;

        if (activity.ParentSpanId != default)
            scope["ParentSpanId"] = activity.ParentSpanId.ToString();

        foreach (var tag in activity.TagObjects)
            scope[tag.Key] = tag.Value;

        return scope;
    }
}
