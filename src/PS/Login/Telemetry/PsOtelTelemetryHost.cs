using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Microsoft.Azure.Cirrus.Cmdlets.Login.Telemetry;

/// <summary>
/// PowerShell-owned concrete <see cref="ITelemetryHost"/>. Wraps the
/// <see cref="TracerProvider"/> built by <see cref="PsTelemetryHostBuilder"/> and is
/// disposed by the module's <c>OnRemove</c> handler.
/// </summary>
/// <remarks>
/// Each host project owns its own concrete implementation. PowerShell's
/// <c>IModuleAssemblyCleanup.OnRemove</c> is synchronous, so a synchronous
/// <see cref="Dispose"/> path is required; <see cref="DisposeAsync"/> delegates to it.
/// </remarks>
internal sealed class PsOtelTelemetryHost : ITelemetryHost, IDisposable
{
    private readonly TracerProvider? _tracerProvider;
    private bool _disposed;

    public ITelemetryService TelemetryService { get; }

    public PsOtelTelemetryHost(ITelemetryService telemetryService, TracerProvider? tracerProvider)
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
