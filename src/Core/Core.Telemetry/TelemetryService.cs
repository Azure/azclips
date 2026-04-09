using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// Implementation of ITelemetryService using OpenTelemetry.
/// Records traces to local files only. Azure Monitor upload is handled
/// asynchronously by a background process via TelemetrySender.
/// </summary>
public class TelemetryService : ITelemetryService
{
    private const string TraceDirectoryName = "Cirrus";

    public static string GetServiceName(ClientType clientType) => clientType switch
    {
        ClientType.CLI => "AzureCLI",
        ClientType.PowerShell => "AzurePS",
        _ => "AzureCLI"
    };

    public static readonly string DefaultServiceVersion =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
        ?? "0.0.0-dev";
    internal const string AzureMonitorConnectionString = "InstrumentationKey=75095e49-0ed7-43bf-9a9b-145ce55963c5;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;ApplicationId=c4b7bab3-d4b6-4f80-8440-0f529a622b2f";

    private readonly TracerProvider _tracerProvider;
    private bool _disposed;

    public ActivitySource ActivitySource { get; }

    /// <summary>
    /// Gets the shared trace directory. Both CLI and PS write here;
    /// the background uploader picks up all files from one location.
    /// </summary>
    public static string GetTraceDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            TraceDirectoryName,
            "traces");

    public TelemetryService(ActivitySource activitySource)
    {
        ActivitySource = activitySource;
        var serviceName = activitySource.Name;
        var moduleVersion = activitySource.Version ?? DefaultServiceVersion;

        var logDirectory = GetTraceDirectory();

        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(serviceName)
            .AddHttpClientInstrumentation(options => // auto-instrument HttpClient calls
            {
                options.RecordException = true;
                options.EnrichWithHttpRequestMessage = (activity, request) =>
                {
                    // user agent is not included by default
                    activity.SetTag("http.request.header.user-agent", request.Headers.UserAgent?.ToString());
                };
            })
            .ConfigureResource(resource =>
                resource.AddService(
                    serviceName: serviceName,
                    serviceVersion: moduleVersion))
            .AddProcessor(new SimpleActivityExportProcessor(new FileExporter(logDirectory)));

#if DEBUG
        // Zipkin exporter for local development. Disable via CIRRUS_ZIPKIN_ENABLED=false.
        if (!string.Equals(Environment.GetEnvironmentVariable("CIRRUS_ZIPKIN_ENABLED"), "false", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddZipkinExporter(options =>
            {
                options.Endpoint = new Uri(Environment.GetEnvironmentVariable("ZIPKIN_ENDPOINT") ?? "http://localhost:9411/api/v2/spans");
            });
        }
#endif

        _tracerProvider = builder.Build()!;
    }

    public Activity StartActivity(string name, ActivityKind kind = ActivityKind.Internal) => ActivitySource.StartActivity(name, kind)!;

    public void ProcessInvocation(ICommandInvocation invocation)
    {
        var activity = Activity.Current;
        if (activity is null) return;

        // Walk up to the root activity so module tags appear on the top-level span
        while (activity.Parent is not null)
            activity = activity.Parent;

        // NOTE: On the CLI side "module.name" currently maps to the extension name.
        // The tag key may be renamed to "extension.name" in the future — TBD.
        if (!string.IsNullOrEmpty(invocation.ModuleName))
            activity.SetTag("module.name", invocation.ModuleName);
        if (!string.IsNullOrEmpty(invocation.ModuleVersion))
            activity.SetTag("module.version", invocation.ModuleVersion);
    }

    public void Flush()
    {
        _tracerProvider?.ForceFlush();
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Skip ForceFlush — FileExporter uses SimpleActivityExportProcessor
        // which exports synchronously inline, so there's nothing to flush.
        // TracerProvider.Dispose() also tries to flush; run it on a thread
        // with a short timeout to avoid blocking on unreachable exporters
        // (e.g. Zipkin in debug mode when no collector is running).
        var disposeTask = Task.Run(() => _tracerProvider?.Dispose());
        disposeTask.Wait(TimeSpan.FromMilliseconds(500));

        // ActivitySource lifetime is managed by the DI container; don't dispose it here.

        _disposed = true;

        GC.SuppressFinalize(this);
    }
}

