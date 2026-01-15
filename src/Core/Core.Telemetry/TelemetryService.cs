using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        _tracerProvider?.ForceFlush();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _tracerProvider?.ForceFlush();
        _tracerProvider?.Dispose();
        ActivitySource?.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
