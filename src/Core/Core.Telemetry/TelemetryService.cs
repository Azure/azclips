using System;
using System.Diagnostics;
using System.IO;
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
        var logFilePath = Path.Combine(logDirectory, $"traces-{DateTime.UtcNow:yyyyMMdd}.log");

        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(serviceName)
            .ConfigureResource(resource =>
                resource.AddService(
                    serviceName: serviceName,
                    serviceVersion: serviceVersion))
            .AddProcessor(new SimpleActivityExportProcessor(new FileExporter(logFilePath)))
            .Build()!;
    }

    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        return ActivitySource.StartActivity(name, kind);
    }

    public void Flush()
    {
        _tracerProvider?.ForceFlush();
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _tracerProvider?.Dispose();
        ActivitySource?.Dispose();
        _disposed = true;
        
        GC.SuppressFinalize(this);
    }
}
