using System;
using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// Implementation of ITelemetryService using OpenTelemetry.
/// Records traces to local files only. Azure Monitor upload is handled
/// asynchronously by a background process via TelemetrySender.
/// </summary>
public class TelemetryService : ITelemetryService
{
    //TODO: Jayson remove this on making PR
    public const string AzureMonitorConnectionString =
        "InstrumentationKey=75095e49-0ed7-43bf-9a9b-145ce55963c5;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;ApplicationId=c4b7bab3-d4b6-4f80-8440-0f529a622b2f";

    private const string TraceDirectoryName = "Cirrus";

    /// <summary>
    /// Gets the shared trace directory. Both CLI and PS write here;
    /// the background uploader picks up all files from one location.
    /// </summary>
    public static string GetTraceDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            TraceDirectoryName,
            "traces");

    private readonly TracerProvider? _tracerProvider;
    private readonly ITelemetrySender _sender;
    private readonly TelemetryOptions _telemetryOptions;
    private bool _disposed;

    public ActivitySource ActivitySource { get; }

    private static readonly AsyncLocal<Activity?> _currentStage = new();

    public Activity? CurrentStage
    {
        get => _currentStage.Value;
        set => _currentStage.Value = value;
    }

    public TelemetryService(ClientType clientType, TelemetryOptions telemetryOptions)
    {
        ActivitySource = new ActivitySource(clientType.ToString());
        _sender = TelemetrySenderFactory.Create(clientType);
        _telemetryOptions = telemetryOptions;

        if (!_telemetryOptions.Enabled)
        {
            return;
        }

        var serviceName = ActivitySource.Name;

        var logDirectory = GetTraceDirectory();

        var builder = Sdk.CreateTracerProviderBuilder().AddSource(serviceName);

        builder.AddHttpClientInstrumentation(options =>
        {
            options.RecordException = true;
            options.EnrichWithHttpRequestMessage = (activity, request) =>
            {
                activity.SetTag("http.request.header.user-agent", request.Headers.UserAgent?.ToString());
            };
        });

        // Let the SDK use its default cloud_RoleName (entry assembly name):
        //   CLI  → "Core.Cli" (or azv2)
        //   PS 7 → "pwsh"
        //   PS 5 → "powershell"

        // Add the appropriate exporter based on client type:
        //   CLI  → FileExporter (buffered to disk, replayed by background process)
        //   PS   → AzureMonitorExporter (inline, no file replay needed)
        _sender.ConfigureExporter(builder, logDirectory);
        _tracerProvider = builder.Build()!;
    }

    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        return ActivitySource.StartActivity(name, kind, CurrentStage?.Context ?? default);
    }

    public void AddProperty(string key, string? value)
    {
        if (string.IsNullOrEmpty(key)) return;
        Activity.Current?.SetTag($"properties.{key}", value);
    }

    public void AddMeasure(string key, double value)
    {
        if (string.IsNullOrEmpty(key)) return;
        Activity.Current?.SetTag($"measures.{key}", value);
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

