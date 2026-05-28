using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// Reusable concrete <see cref="ITelemetryHost"/> implementation owning a
/// <see cref="TracerProvider"/>. Each host project (<c>CliTelemetryHost</c>,
/// <c>TuiTelemetryHost</c>, <c>PsTelemetryHost</c>) composes its OpenTelemetry pipeline
/// in its own fluent builder and wraps the resulting <see cref="TracerProvider"/> in
/// one of these so the dispose / flush dance is implemented exactly once.
/// </summary>
/// <remarks>
/// Implements both <see cref="IDisposable"/> and <see cref="IAsyncDisposable"/> so the
/// host can be torn down from CLI / TUI <c>await</c>-able code and from PowerShell's
/// synchronous <c>IModuleAssemblyCleanup.OnRemove</c>. <see cref="IDisposable.Dispose"/>
/// and <see cref="IAsyncDisposable.DisposeAsync"/> share the same body — the work is
/// CPU-bound (synchronous <c>TracerProvider.Dispose()</c>) so there is no genuine async
/// path to expose.
/// </remarks>
public sealed class OtelTelemetryHost : ITelemetryHost, IDisposable
{
    private readonly TracerProvider? _tracerProvider;
    private bool _disposed;

    public ITelemetryService TelemetryService { get; }

    private OtelTelemetryHost(ITelemetryService telemetryService, TracerProvider? tracerProvider)
    {
        TelemetryService = telemetryService;
        _tracerProvider = tracerProvider;
    }

    /// <summary>
    /// Creates a host that exports activities through the given <paramref name="tracerProvider"/>.
    /// Pass <c>null</c> when telemetry is disabled — the host then acts as a no-op
    /// (activities started on the recorder are silently dropped).
    /// </summary>
    public static OtelTelemetryHost Create(ITelemetryService telemetryService, TracerProvider? tracerProvider)
        => new(telemetryService, tracerProvider);

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        _tracerProvider?.ForceFlush();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Skip ForceFlush — FileExporter uses SimpleActivityExportProcessor which exports
        // synchronously inline, so there is nothing to flush. TracerProvider.Dispose() also
        // tries to flush; run it on a thread with a short timeout to avoid blocking on
        // unreachable exporters (e.g. Zipkin in debug mode when no collector is running).
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
