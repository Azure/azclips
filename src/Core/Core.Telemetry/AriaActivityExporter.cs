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
/// <remarks>
/// Must be wired via <see cref="SimpleActivityExportProcessor"/>, not a batching processor.
/// The dynamic-destination constructor reads <see cref="ITelemetryRoutingContext.Current"/>
/// inside <see cref="Export"/> to pick the endpoint for that call — this is only correct
/// because Simple processors call Export synchronously, inline, on the very async flow that
/// ended the Activity (the same flow that set the routing context for that command). A
/// batching processor flushes on its own background timer, whose ambient AsyncLocal context
/// has nothing to do with whichever command originally produced each buffered Activity.
/// </remarks>
internal sealed class AriaActivityExporter : BaseExporter<Activity>
{
    private static readonly EventId CommandEvent = new(1, "CommandExecution");
    private readonly ILoggerFactory _globalLoggerFactory;
    private readonly ILogger _globalLogger;
    private readonly ILoggerFactory? _euLoggerFactory;
    private readonly ILogger? _euLogger;
    private readonly ITelemetryRoutingContext? _routingContext;

    /// <summary>
    /// Fixed-destination constructor. Every batch this instance exports goes to the single
    /// endpoint resolved up front. Correct when the caller already knows the destination for
    /// this exporter's entire lifetime — e.g. the CLI's buffered upload replays one
    /// already-boundary-partitioned group per exporter instance.
    /// </summary>
    public AriaActivityExporter(string connectionString, Uri? ingestionEndpoint = null)
    {
        _globalLoggerFactory = BuildLoggerFactory(connectionString, ingestionEndpoint);
        _globalLogger = _globalLoggerFactory.CreateLogger("Azclips");
    }

    /// <summary>
    /// Dynamic-destination constructor. Builds both the Global and EU loggers up front and
    /// selects between them per export call by reading <paramref name="routingContext"/>.
    /// Required for an exporter built once and reused across many command invocations that
    /// may carry different tenants/boundaries over its lifetime (PowerShell's singleton
    /// TracerProvider) — the fixed-destination constructor would freeze whatever boundary
    /// was ambient at construction (module import) time and never re-check it.
    /// </summary>
    public AriaActivityExporter(ITelemetryRoutingContext routingContext)
    {
        _routingContext = routingContext ?? throw new ArgumentNullException(nameof(routingContext));
        _globalLoggerFactory = BuildLoggerFactory(WellKnownEndpoints.AriaConnectionString, null);
        _globalLogger = _globalLoggerFactory.CreateLogger("Azclips");
        _euLoggerFactory = BuildLoggerFactory(WellKnownEndpoints.AriaEuConnectionString, WellKnownEndpoints.AriaEuEndpoint);
        _euLogger = _euLoggerFactory.CreateLogger("Azclips");
    }

    private static ILoggerFactory BuildLoggerFactory(string connectionString, Uri? ingestionEndpoint) =>
        LoggerFactory.Create(logging =>
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

    public override ExportResult Export(in Batch<Activity> batch)
    {
        // Resolved once per Export call (not once per exporter instance) so a long-lived
        // exporter still reflects each ambient invocation's boundary correctly. Safe only
        // under SimpleActivityExportProcessor — see the type-level remarks.
        var logger = SelectLogger();

        foreach (var activity in batch)
        {
            // Skip HTTP instrumentation spans — only export application-level spans.
            if (activity.Source.Name == "System.Net.Http")
                continue;

            using (logger.BeginScope(BuildScope(activity)))
            {
                if (activity.Status == ActivityStatusCode.Error)
                {
                    logger.LogError(CommandEvent, "{OperationName}", activity.OperationName);
                }
                else
                {
                    logger.LogInformation(CommandEvent, "{OperationName}", activity.OperationName);
                }
            }
        }

        return ExportResult.Success;
    }

    private ILogger SelectLogger()
    {
        if (_routingContext is null || _euLogger is null)
            return _globalLogger;

        return DataBoundaryRouting.IsEu(_routingContext.Current.DataBoundary)
            ? _euLogger
            : _globalLogger;
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        _globalLoggerFactory.Dispose();
        _euLoggerFactory?.Dispose();
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _globalLoggerFactory.Dispose();
            _euLoggerFactory?.Dispose();
        }
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

        if (activity.ParentSpanId != default)
            scope["ParentSpanId"] = activity.ParentSpanId.ToString();

        foreach (var tag in activity.TagObjects)
            scope[tag.Key] = tag.Value;

        return scope;
    }
}
