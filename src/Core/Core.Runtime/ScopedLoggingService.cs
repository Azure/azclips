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
    private readonly IConfigurationService? _configurationService;
    private readonly ILoggingService _fallback;

    public ScopedLoggingService(IConfigurationService? configurationService = null)
        : this(configurationService, new ConsoleLoggingService())
    {
    }

    internal ScopedLoggingService(IConfigurationService? configurationService, ILoggingService fallback)
    {
        _configurationService = configurationService;
        _fallback = fallback;
    }

    public void Log(string message) => Write(message, LogSeverity.Info, static (logger, value) => logger.Log(value));
    public void Info(string message) => Write(message, LogSeverity.Info, static (logger, value) => logger.Info(value));
    public void Verbose(string message) => Write(message, LogSeverity.Verbose, static (logger, value) => logger.Verbose(value));
    public void Debug(string message) => Write(message, LogSeverity.Debug, static (logger, value) => logger.Debug(value));
    public void Warn(string message) => Write(message, LogSeverity.Warn, static (logger, value) => logger.Warn(value));
    public void Error(string message) => Write(message, LogSeverity.Error, static (logger, value) => logger.Error(value));

    private void Write(string message, LogSeverity severity, Action<ILoggingService, string> write)
    {
        if (_current.Value is { } current)
        {
            write(current, message);
            return;
        }

        var configuredLevel = _configurationService?
            .GetConfiguration<CoreOptions>()
            .Logging.Level;
        if (ShouldEmit(configuredLevel, severity))
        {
            write(_fallback, message);
        }
    }

    private static bool ShouldEmit(string? configuredLevel, LogSeverity severity)
    {
        var level = configuredLevel?.Trim().ToLowerInvariant() switch
        {
            "error" => LogLevel.Error,
            "info" => LogLevel.Info,
            "debug" => LogLevel.Debug,
            _ => LogLevel.Warn,
        };

        return severity switch
        {
            LogSeverity.Error => true,
            LogSeverity.Warn => level >= LogLevel.Warn,
            LogSeverity.Info => level >= LogLevel.Info,
            LogSeverity.Debug or LogSeverity.Verbose => level >= LogLevel.Debug,
            _ => false,
        };
    }

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

    private enum LogLevel
    {
        Error,
        Warn,
        Info,
        Debug,
    }

    private enum LogSeverity
    {
        Error,
        Warn,
        Info,
        Debug,
        Verbose,
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