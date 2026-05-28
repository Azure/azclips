namespace Core.Abstractions;

/// <summary>
/// Owns the OpenTelemetry pipeline lifecycle (TracerProvider, exporters, instrumentation)
/// for a single host process.
/// </summary>
/// <remarks>
/// The host builds the pipeline at startup and disposes it on shutdown to guarantee
/// buffered traces are flushed. Each entry point (CLI, TUI, PowerShell module) provides
/// its own implementation tuned to its lifecycle:
/// <list type="bullet">
///   <item><description>CLI: short-lived process, file-buffered sender, uploader spawned on dispose.</description></item>
///   <item><description>TUI: long-running session, periodic flush, direct Azure Monitor export.</description></item>
///   <item><description>PowerShell: lifetime tied to module import/unload.</description></item>
/// </list>
/// All implementations share the same <see cref="ITelemetryRecorder"/> contract so the rest
/// of the system depends only on recording, not on pipeline construction.
/// </remarks>
public interface ITelemetryHost : IAsyncDisposable
{
    /// <summary>
    /// The recorder bound to this host's pipeline. Activities started via this recorder
    /// are observed by the TracerProvider configured by the host.
    /// </summary>
    ITelemetryService TelemetryService { get; }

    /// <summary>
    /// Flushes any buffered telemetry. Hosts should call this before performing side effects
    /// that depend on traces being persisted (e.g. the CLI spawning an uploader child process).
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
