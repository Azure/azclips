using Core.Abstractions;
using Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Cli.Telemetry;

/// <summary>
/// CLI host wiring for the OpenTelemetry pipeline. The CLI is a short-lived process,
/// so traces are buffered to disk via <see cref="FileTelemetrySender"/> and the
/// <c>--upload-telemetry</c> child process replays them to Azure Monitor asynchronously
/// — letting the CLI exit immediately without waiting on network IO.
/// </summary>
/// <remarks>
/// The lifecycle wrapper is <see cref="CliOtelTelemetryHost"/>, owned by this project.
/// The builder produces an <see cref="ITelemetryHost"/> so consumers depend only on the
/// abstraction; per-host policy (timeouts, flush ordering) lives inside the concrete.
/// </remarks>
public static class CliTelemetryHost
{
    /// <summary>
    /// Creates a builder for a CLI telemetry host bound to the given recorder.
    /// </summary>
    public static CliTelemetryHostBuilder CreateBuilder(ITelemetryService telemetryService, TelemetryOptions telemetryOptions)
        => new(telemetryService, telemetryOptions);
}

/// <summary>
/// Fluent builder for the CLI's <see cref="ITelemetryHost"/>. Builder methods correspond
/// to CLI-specific composition choices (which sender, which instrumentation); each can
/// be flipped without editing shared <c>Core.Telemetry</c> code.
/// </summary>
public sealed class CliTelemetryHostBuilder
{
    private readonly ITelemetryService _telemetryService;
    private readonly TelemetryOptions _telemetryOptions;
    private ITelemetrySender? _sender;
    private string? _senderDirectory;

    internal CliTelemetryHostBuilder(ITelemetryService telemetryService, TelemetryOptions telemetryOptions)
    {
        _telemetryService = telemetryService;
        _telemetryOptions = telemetryOptions;
    }

    /// <summary>
    /// Wires the file-based sender. The CLI always uses the file sender so the
    /// short-lived process can return immediately; a separate <c>--upload-telemetry</c>
    /// child process replays the traces to Azure Monitor in the background.
    /// </summary>
    public CliTelemetryHostBuilder UseFileSender(string traceDirectory)
    {
        _sender = new FileTelemetrySender();
        _senderDirectory = traceDirectory;
        return this;
    }

    public ITelemetryHost Build()
    {
        if (!_telemetryOptions.Enabled)
        {
            return new CliOtelTelemetryHost(_telemetryService, tracerProvider: null);
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
        var tracerProvider = builder.Build()!;
        return new CliOtelTelemetryHost(_telemetryService, tracerProvider);
    }
}

