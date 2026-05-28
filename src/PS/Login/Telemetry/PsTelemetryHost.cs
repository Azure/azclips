using Core.Abstractions;
using Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.Azure.Cirrus.Cmdlets.Login.Telemetry;

public static class PsTelemetryHost
{
    /// <summary>
    /// Creates a builder for a PS telemetry host bound to the given recorder.
    /// </summary>
    public static PsTelemetryHostBuilder CreateBuilder(ITelemetryService telemetryService, TelemetryOptions telemetryOptions)
        => new(telemetryService, telemetryOptions);
}

public sealed class PsTelemetryHostBuilder
{
    private readonly ITelemetryService _telemetryService;
    private readonly TelemetryOptions _telemetryOptions;
    private ITelemetrySender? _sender;

    internal PsTelemetryHostBuilder(ITelemetryService telemetryService, TelemetryOptions telemetryOptions)
    {
        _telemetryService = telemetryService;
        _telemetryOptions = telemetryOptions;
    }

    /// <summary>
    /// Wires the inline Azure Monitor sender. PS cmdlets are discrete invocations
    /// without a follow-up uploader process, so traces are exported inline.
    /// </summary>
    public PsTelemetryHostBuilder UseDirectSender()
    {
        _sender = new DirectTelemetrySender();
        return this;
    }

    public OtelTelemetryHost Build()
    {
        if (!_telemetryOptions.Enabled)
        {
            return OtelTelemetryHost.Create(_telemetryService, tracerProvider: null);
        }

        if (_sender is null)
        {
            throw new InvalidOperationException(
                "A sender must be configured before building the host. Call UseDirectSender.");
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

        // DirectTelemetrySender.ConfigureExporter takes the sender directory parameter but
        // ignores it for direct export — pass an empty path to satisfy the interface.
        _sender.ConfigureExporter(builder, traceDirectory: string.Empty);
        var tracerProvider = builder.Build()!;
        return OtelTelemetryHost.Create(_telemetryService, tracerProvider);
    }
}
