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
    internal const string DefaultServiceName = "AzureCLI";
    internal static readonly string DefaultServiceVersion =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
        ?? "0.0.0-dev";
    internal const string AzureMonitorConnectionString = "InstrumentationKey=75095e49-0ed7-43bf-9a9b-145ce55963c5;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;ApplicationId=c4b7bab3-d4b6-4f80-8440-0f529a622b2f";

    private readonly TracerProvider _tracerProvider;
    private bool _disposed;

    public ActivitySource ActivitySource { get; }

    /// <summary>
    /// Gets the directory where trace files are stored.
    /// </summary>
    public static string GetTraceDirectory(string serviceName = DefaultServiceName) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            serviceName,
            "traces");

    public TelemetryService(string serviceName = DefaultServiceName)
    {
        var moduleVersion = DefaultServiceVersion;
        ActivitySource = new ActivitySource(serviceName, moduleVersion);

        var logDirectory = GetTraceDirectory(serviceName);

        _tracerProvider = Sdk.CreateTracerProviderBuilder()
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
            .AddProcessor(new SimpleActivityExportProcessor(new FileExporter(logDirectory)))
#if DEBUG
            .AddZipkinExporter(options =>
            {
                options.Endpoint = new Uri(Environment.GetEnvironmentVariable("ZIPKIN_ENDPOINT") ?? "http://localhost:9411/api/v2/spans");
            })
#endif
            .Build()!;
    }

    public Activity StartActivity(string name, ActivityKind kind = ActivityKind.Internal) => ActivitySource.StartActivity(name, kind)!;

    public void ProcessInvocation(ICommandInvocation invocation)
    {
        var activity = Activity.Current;
        if (activity is null) return;

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

        ActivitySource?.Dispose();

        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
