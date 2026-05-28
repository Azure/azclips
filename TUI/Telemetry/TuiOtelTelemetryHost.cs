using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Tui.Telemetry;

/// <summary>
/// TUI-owned concrete <see cref="ITelemetryHost"/>. Wraps the <see cref="TracerProvider"/>
/// built by <see cref="TuiTelemetryHostBuilder"/> and disposes it when the TUI session
/// ends.
/// </summary>
internal sealed class TuiOtelTelemetryHost : ITelemetryHost, IDisposable
{
    private readonly TracerProvider? _tracerProvider;
    private bool _disposed;

    public ITelemetryService TelemetryService { get; }

    public TuiOtelTelemetryHost(ITelemetryService telemetryService, TracerProvider? tracerProvider)
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
