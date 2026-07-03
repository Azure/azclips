// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Runtime;

using Core.Abstractions;

// Registered as singleton — injected into all services normally.
// Uses AsyncLocal to provide per-invocation logging without changing
// any service's constructor signature. Each async flow (e.g., a PS
// runspace or CLI command) sees its own ILoggingService via UseScope(),
// while services that run outside a session fall back to console logging.
public class ScopedLoggingService : ILoggingService
{
    // AsyncLocal stores a value per async control flow (ExecutionContext).
    // Unlike ThreadLocal/[ThreadStatic], the value flows across awaits —
    // so after "await SomeAsync()" resumes on a different thread-pool thread,
    // the same session logger is still visible. Concurrent sessions on
    // different async flows are fully isolated: setting the value in one
    // flow does not affect another.
    private static readonly AsyncLocal<ILoggingService?> _current = new();
    private readonly ILoggingService _fallback;

    public ScopedLoggingService()
    {
        // Default behavior when no session is active (startup, background work)
        _fallback = new ConsoleLoggingService();
    }

    private ILoggingService Active => _current.Value ?? _fallback;

    public void Log(string message) => Active.Log(message);
    public void Info(string message) => Active.Info(message);
    public void Verbose(string message) => Active.Verbose(message);
    public void Debug(string message) => Active.Debug(message);
    public void Warn(string message) => Active.Warn(message);
    public void Error(string message) => Active.Error(message);

    /// <summary>
    /// Sets the per-session logger for the current async flow.
    /// Returns a disposable that restores the previous logger.
    /// </summary>
    public static IDisposable UseScope(ILoggingService sessionLogger)
    {
        var previous = _current.Value;
        _current.Value = sessionLogger;
        return new Scope(previous);
    }

    private sealed class Scope(ILoggingService? previous) : IDisposable
    {
        public void Dispose() => _current.Value = previous;
    }
}

public class OutputStreamAdapter: ILoggingService
{
    private readonly OutputStreams _outputStreams;
    private readonly CancellationToken _cancellationToken;
    public OutputStreamAdapter(OutputStreams outputStreams, CancellationToken cancellationToken = default)
    {
        _outputStreams = outputStreams;
        _cancellationToken = cancellationToken;
    }

    public void Log(string message)
    {
        _outputStreams.WriteInformation(message, _cancellationToken).GetAwaiter().GetResult();
    }

    public void Info(string message)
    {
        _outputStreams.WriteInformation(message, _cancellationToken).GetAwaiter().GetResult();
    }

    public void Debug(string message)
    {
        _outputStreams.WriteDebug(message, _cancellationToken).GetAwaiter().GetResult();
    }

    public void Verbose(string message)
    {
        _outputStreams.WriteVerbose(message, _cancellationToken).GetAwaiter().GetResult();
    }

    public void Warn(string message)
    {
        _outputStreams.WriteWarning(message, _cancellationToken).GetAwaiter().GetResult();
    }

    public void Error(string message)
    {
        _outputStreams.WriteError(message, _cancellationToken).GetAwaiter().GetResult();
    }
}