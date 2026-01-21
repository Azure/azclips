using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Azure.Monitor.OpenTelemetry.Exporter;
using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// Implementation of ITelemetryService using OpenTelemetry.
/// </summary>
public class TelemetryService : ITelemetryService
{
    private readonly TracerProvider _tracerProvider;
    private bool _disposed;

    /// <summary>
    /// Whether to flush telemetry synchronously on dispose.
    /// Set to false (default) for faster CLI exit. Telemetry is sent in background.
    /// Set to true to ensure all telemetry is sent before process exits.
    /// </summary>
    public bool SyncFlushOnDispose { get; set; } = false;

    /// <summary>
    /// Timeout in milliseconds for flush operations.
    /// </summary>
    private const int FlushTimeoutMs = 5000;

    public ActivitySource ActivitySource { get; }

    public TelemetryService(string serviceName = "AzureCLI", string serviceVersion = "1.0.0")
    {
        ActivitySource = new ActivitySource(serviceName, serviceVersion);

        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            serviceName,
            "traces"
        );

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
                    serviceVersion: serviceVersion))
            .AddProcessor(new SimpleActivityExportProcessor(new FileExporter(logDirectory)))
            .AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString = "InstrumentationKey=75095e49-0ed7-43bf-9a9b-145ce55963c5;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;ApplicationId=c4b7bab3-d4b6-4f80-8440-0f529a622b2f";
            })
#if DEBUG
            .AddZipkinExporter(options =>
            {
                options.Endpoint = new Uri(Environment.GetEnvironmentVariable("ZIPKIN_ENDPOINT") ?? "http://localhost:9411/api/v2/spans");
            })
#endif
            .Build()!;
    }

    public void Flush()
    {
        _tracerProvider?.ForceFlush(FlushTimeoutMs);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (SyncFlushOnDispose)
        {
            // Synchronous flush - blocks until all data is sent
            _tracerProvider?.ForceFlush(FlushTimeoutMs);
            _tracerProvider?.Dispose();
            ActivitySource?.Dispose();
        }
        else
        {
            // Async flush - CLI exits immediately, telemetry sent in background
            // Note: Some telemetry may be lost if process exits before completion
            var provider = _tracerProvider;
            var source = ActivitySource;

            Task.Run(() =>
            {
                try
                {
                    provider?.ForceFlush(FlushTimeoutMs);
                    provider?.Dispose();
                    source?.Dispose();
                }
                catch
                {
                    // Ignore errors during background cleanup
                }
            });
        }

        GC.SuppressFinalize(this);
    }
}
