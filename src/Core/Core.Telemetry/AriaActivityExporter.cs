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
    private static readonly ITelemetryPropertyFilter AllowedScopeTags = new DefaultTelemetryPropertyFilter();
    private readonly ILoggerFactory _globalLoggerFactory;
    private readonly ILogger _globalLogger;
    private readonly ILoggerFactory? _euLoggerFactory;
    private readonly ILogger? _euLogger;
    private readonly bool _dynamicRouting;
    private int _loggerFactoriesDisposed;

    /// <summary>
    /// Fixed-destination constructor. Every batch this instance exports goes to the single
    /// endpoint resolved up front. Correct when the caller already knows the destination for
    /// this exporter's entire lifetime — e.g. the CLI's buffered upload replays one
    /// already-boundary-partitioned group per exporter instance.
    /// </summary>
    public AriaActivityExporter(string connectionString, Uri? ingestionEndpoint = null)
        : this(connectionString, ingestionEndpoint, BuildLoggerFactory)
    {
    }

    internal AriaActivityExporter(
        string connectionString,
        Uri? ingestionEndpoint,
        Func<string, Uri?, ILoggerFactory> loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _globalLoggerFactory = loggerFactory(connectionString, ingestionEndpoint);
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
        : this(BuildLoggerFactory)
    {
    }

    internal AriaActivityExporter(Func<string, Uri?, ILoggerFactory> loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _dynamicRouting = true;
        _globalLoggerFactory = loggerFactory(WellKnownEndpoints.AriaConnectionString, WellKnownEndpoints.AriaRowEndpoint);
        _globalLogger = _globalLoggerFactory.CreateLogger("Azclips");
        _euLoggerFactory = loggerFactory(WellKnownEndpoints.AriaEuConnectionString, WellKnownEndpoints.AriaEuEndpoint);
        _euLogger = _euLoggerFactory.CreateLogger("Azclips");
    }

    private static ILoggerFactory BuildLoggerFactory(string connectionString, Uri? ingestionEndpoint)
    {
        TelemetryNetworkGuard.EnsureAllowed();
        return LoggerFactory.Create(logging =>
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
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (var activity in batch)
        {
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

        // Fail-closed: only a CONFIRMED Global boundary routes to the ROW logger; EU, missing,
        // and unrecognized values all stay on the EU logger (see DataBoundaryRouting.IsConfirmedGlobal).
        return DataBoundaryRouting.IsConfirmedGlobal(ResolveDataBoundary(activity))
            ? _globalLogger
            : _euLogger;
    }

    /// <summary>
    /// Walks from <paramref name="activity"/> up through its parents to find the
    /// <see cref="TelemetryTags.DataBoundary"/> tag — set once on the root by whichever flow
    /// resolved it (see type-level remarks). Independent of ambient <see cref="Activity.Current"/>,
    /// which may differ when replay or callbacks export the activity.
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
        DisposeLoggerFactories();
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            DisposeLoggerFactories();
        base.Dispose(disposing);
    }

    private void DisposeLoggerFactories()
    {
        if (Interlocked.Exchange(ref _loggerFactoriesDisposed, 1) != 0)
            return;

        _globalLoggerFactory.Dispose();
        _euLoggerFactory?.Dispose();
    }

    internal static Dictionary<string, object?> BuildScope(Activity activity)
    {
        var scope = new Dictionary<string, object?>
        {
            ["traceId"] = activity.TraceId.ToString(),
            ["spanId"] = activity.SpanId.ToString(),
            ["durationMs"] = activity.Duration.TotalMilliseconds,
            ["startTime"] = activity.StartTimeUtc.ToString("o"),
            ["status"] = activity.Status.ToString(),
            ["kind"] = activity.Kind.ToString(),
        };

        if (activity.ParentSpanId != default)
            scope["parentSpanId"] = activity.ParentSpanId.ToString();

        // Child activities do not inherit parent tags in System.Diagnostics.Activity — merge
        // the resolved parent-chain tags (root-to-leaf order, so a child's own tag wins on a
        // name collision) into the scope, so a child's Aria record still carries the root's
        // TenantId/DataBoundary/SubscriptionId metadata. FileExporter does the equivalent via
        // its own per-tag ResolveTag walk for persisted traces.
        //
        // Filtered through the same allowlist DefaultTelemetryPropertyFilterProcessor uses:
        // that processor only strips disallowed tags off an Activity when THAT Activity itself
        // ends (OnEnd), so an ancestor still open at the time a child exports has not been
        // filtered yet — merging its raw TagObjects here would bypass filtering entirely for
        // whatever it happens to carry (the allowlist explicitly excludes URL/path data).
        var chain = new Stack<Activity>();
        for (var current = activity; current is not null; current = current.Parent)
            chain.Push(current);

        while (chain.TryPop(out var current))
        {
            foreach (var tag in current.TagObjects)
            {
                if (AllowedScopeTags.IsAllowed(tag.Key))
                    scope[tag.Key] = tag.Value;
            }
        }

        return scope;
    }
}
