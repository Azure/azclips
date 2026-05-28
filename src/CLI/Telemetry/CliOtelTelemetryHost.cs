using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Cli.Telemetry;

/// <summary>
/// CLI-owned concrete <see cref="ITelemetryHost"/>. Wraps the <see cref="TracerProvider"/>
/// built by <see cref="CliTelemetryHostBuilder"/> and is responsible for flushing /
/// disposing it when the CLI process exits.
/// </summary>
/// <remarks>
/// Each host project owns its own concrete implementation so that the lifecycle policy
/// (timeouts, flush-before-spawn-uploader, etc.) can diverge per host without touching
/// shared <c>Core.Telemetry</c> code. Host projects must not depend on a shared concrete
/// host type — they depend only on the <see cref="ITelemetryHost"/> abstraction.
/// </remarks>
internal sealed class CliOtelTelemetryHost : ITelemetryHost, IDisposable
{
    private readonly TracerProvider? _tracerProvider;
    private bool _disposed;

    public ITelemetryService TelemetryService { get; }

    public CliOtelTelemetryHost(ITelemetryService telemetryService, TracerProvider? tracerProvider)
    {
        TelemetryService = telemetryService;
        _tracerProvider = tracerProvider;
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        _tracerProvider?.ForceFlush();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // FileTelemetrySender uses SimpleActivityExportProcessor (synchronous inline
        // export), so there is nothing to ForceFlush. TracerProvider.Dispose() itself
        // attempts a final flush; run it on a worker with a bounded timeout so an
        // unreachable exporter (e.g. Zipkin in debug with no collector) cannot block
        // CLI shutdown.
        if (_tracerProvider is not null)
        {
            var disposeTask = Task.Run(() => _tracerProvider.Dispose());
            disposeTask.Wait(TimeSpan.FromMilliseconds(500));
        }

        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
