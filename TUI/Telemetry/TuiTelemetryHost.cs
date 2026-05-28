using Core.Abstractions;
using Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Tui.Telemetry;

/// <summary>
/// TUI host wiring for the OpenTelemetry pipeline. The TUI is a long-running interactive
/// process; traces are written to disk via <see cref="FileTelemetrySender"/>, preserving
/// the historical TUI behaviour. Replaying those traces to Azure Monitor is out of scope
/// for this pass.
/// </summary>
public static class TuiTelemetryHost
{
    /// <summary>
    /// Creates a builder for a TUI telemetry host bound to the given recorder.
    /// </summary>
    public static TuiTelemetryHostBuilder CreateBuilder(ITelemetryService telemetryService, TelemetryOptions telemetryOptions)
        => new(telemetryService, telemetryOptions);
}

public sealed class TuiTelemetryHostBuilder
{
    private readonly ITelemetryService _telemetryService;
    private readonly TelemetryOptions _telemetryOptions;
    private ITelemetrySender? _sender;
    private string? _senderDirectory;

    internal TuiTelemetryHostBuilder(ITelemetryService telemetryService, TelemetryOptions telemetryOptions)
    {
        _telemetryService = telemetryService;
        _telemetryOptions = telemetryOptions;
    }

    /// <summary>
    /// Wires the file-based sender. Preserves the pre-refactor TUI behaviour where
    /// telemetry was buffered to <see cref="FileTelemetrySender.GetTraceDirectory"/> for
    /// out-of-band replay.
    /// </summary>
    public TuiTelemetryHostBuilder UseFileSender(string traceDirectory)
    {
        _sender = new FileTelemetrySender();
        _senderDirectory = traceDirectory;
        return this;
    }

    public OtelTelemetryHost Build()
    {
        if (!_telemetryOptions.Enabled)
        {
            return OtelTelemetryHost.Create(_telemetryService, tracerProvider: null);
        }

        if (_sender is null || _senderDirectory is null)
        {
            throw new InvalidOperationException(
                "A sender must be configured before building the host. Call UseFileSender.");
        }

        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(_telemetryService.ActivitySource.Name);

        builder.AddHttpClientInstrumentation(options =>
        {
            options.RecordException = true;
            options.EnrichWithHttpRequestMessage = (activity, request) =>
            {
                activity.SetTag("http.request.header.user-agent", request.Headers.UserAgent?.ToString());
            };
        });

        _sender.ConfigureExporter(builder, _senderDirectory);
        var tracerProvider = builder.Build();
        return OtelTelemetryHost.Create(_telemetryService, tracerProvider);
    }
}
