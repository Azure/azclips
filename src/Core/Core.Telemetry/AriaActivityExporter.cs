// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter.OneCollector;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace Core.Telemetry;

/// <summary>
/// Exports completed Activities to Aria through OneCollector.
/// </summary>
/// <remarks>
/// Must be wired via <see cref="SimpleActivityExportProcessor"/>, not a batching processor.
/// Live routing reads boundary and outcome tags from each Activity's parent chain.
/// Shared Activity tags remain visible across worker flows, unlike AsyncLocal writes.
/// </remarks>
internal sealed class AriaActivityExporter : BaseExporter<Activity>
{
    private static readonly EventId CommandEvent = new(1, "CommandExecution");
    private const string ServiceName = "AzCLIPS";
    private static readonly DefaultTelemetryPropertyFilter AllowedScopeTags = new();
    private readonly ILoggerFactory _globalLoggerFactory;
    private readonly ILogger _globalLogger;
    private readonly ILoggerFactory? _euLoggerFactory;
    private readonly ILogger? _euLogger;
    private readonly bool _dynamicRouting;
    private int _loggerFactoriesDisposed;

    /// <summary>
    /// Fixed destination for replay groups partitioned by boundary and outcome.
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
    /// Dynamic destination for live providers reused across invocations.
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
                otel.SetResourceBuilder(ResourceBuilder.CreateEmpty().AddTelemetrySdk().AddService(ServiceName));
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
            var logger = SelectLogger(activity);

            using (logger.BeginScope(BuildScope(activity)))
            {
                if (activity.Status == ActivityStatusCode.Error)
                {
                    logger.LogError(CommandEvent, "{operationName}", activity.OperationName);
                }
                else
                {
                    logger.LogInformation(CommandEvent, "{operationName}", activity.OperationName);
                }
            }
        }

        return ExportResult.Success;
    }

    private ILogger SelectLogger(Activity activity)
    {
        if (!_dynamicRouting || _euLogger is null)
            return _globalLogger;

        return DataBoundaryRouting.ResolveDestination(
            ResolveTag(activity, TelemetryTags.DataBoundary),
            ResolveTag(activity, TelemetryTags.DataBoundaryOutcome)) == RegionalDestination.Row
            ? _globalLogger
            : _euLogger;
    }

    /// <summary>
    /// Resolves a routing tag without depending on ambient Activity.Current.
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

        // Child activities do not inherit parent tags. Merge root-to-leaf so the child wins,
        // while applying the allowlist because open ancestors have not yet passed through the
        // filtering processor. FileExporter uses the same merge before parent objects are lost.
        AllowedScopeTags.MergeAllowedTags(activity, scope);

        return scope;
    }
}
