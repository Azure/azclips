// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
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
/// The dynamic-destination constructor resolves the endpoint per exported Activity by reading
/// <see cref="TelemetryTags.DataBoundary"/> off that Activity's own tag (walking up its parent
/// chain to the root, since the tag is set once on the root — see
/// <see cref="Core.Abstractions.TelemetryTags.DataBoundary"/>). This is deliberately NOT read
/// from ambient async-local state: the boundary is often resolved on a different async flow
/// than the one that created the root activity (e.g. PowerShell's <c>ExecutionSession.Run()</c>
/// resolves it on a forked worker task, while the cmdlet's own flow disposes the root), and a
/// write to AsyncLocal-backed state made on that forked flow would never be visible back on the
/// disposing flow. Activity tags don't have this problem: they mutate a shared heap object that
/// any code holding a reference to the same Activity (or its descendants) can see.
/// </remarks>
internal sealed class AriaActivityExporter : BaseExporter<Activity>
{
    private static readonly EventId CommandEvent = new(1, "CommandExecution");
    private readonly ILoggerFactory _globalLoggerFactory;
    private readonly ILogger _globalLogger;
    private readonly ILoggerFactory? _euLoggerFactory;
    private readonly ILogger? _euLogger;
    private readonly bool _dynamicRouting;

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
    /// selects between them per exported Activity, by reading that Activity's own
    /// <see cref="TelemetryTags.DataBoundary"/> tag (see type-level remarks). Required for an
    /// exporter built once and reused across many command invocations that may carry different
    /// tenants/boundaries over its lifetime (PowerShell's singleton TracerProvider) — the
    /// fixed-destination constructor would freeze whatever boundary was ambient at construction
    /// (module import) time and never re-check it.
    /// </summary>
    public AriaActivityExporter()
    {
        _dynamicRouting = true;
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
        foreach (var activity in batch)
        {
            // Skip HTTP instrumentation spans — only export application-level spans.
            if (activity.Source.Name == "System.Net.Http")
                continue;

            // Resolved per activity (not once per Export call): each activity in principle
            // could belong to a different invocation/boundary, and this exporter instance may
            // be reused across many invocations (PowerShell's singleton TracerProvider).
            var logger = SelectLogger(activity);

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

    private ILogger SelectLogger(Activity activity)
    {
        if (!_dynamicRouting || _euLogger is null)
            return _globalLogger;

        return DataBoundaryRouting.IsEu(ResolveDataBoundary(activity))
            ? _euLogger
            : _globalLogger;
    }

    /// <summary>
    /// Walks from <paramref name="activity"/> up through its parents to find the
    /// <see cref="TelemetryTags.DataBoundary"/> tag — set once on the root by whichever flow
    /// resolved it (see type-level remarks). Independent of ambient <see cref="Activity.Current"/>,
    /// which may already differ by the time an exporter runs (Activity.Stop() restores Current
    /// to the parent before OnEnd/Export is invoked).
    /// </summary>
    private static string? ResolveDataBoundary(Activity activity)
    {
        for (var current = activity; current is not null; current = current.Parent)
        {
            if (current.GetTagItem(TelemetryTags.DataBoundary) is string boundary && !string.IsNullOrEmpty(boundary))
                return boundary;
        }
        return null;
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
