// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Runtime;

public sealed class LevelFilteredLoggingService : ILoggingService
{
    private readonly ILoggingService _inner;
    private readonly LogLevel _level;

    public LevelFilteredLoggingService(ILoggingService inner, string? configuredLevel)
    {
        _inner = inner;
        _level = Parse(configuredLevel);
    }

    public void Log(string message) => Info(message);

    public void Info(string message)
    {
        if (_level >= LogLevel.Info)
        {
            _inner.Info(message);
        }
    }

    public void Verbose(string message)
    {
        if (_level >= LogLevel.Debug)
        {
            _inner.Verbose(message);
        }
    }

    public void Debug(string message)
    {
        if (_level >= LogLevel.Debug)
        {
            _inner.Debug(message);
        }
    }

    public void Warn(string message)
    {
        if (_level >= LogLevel.Warn)
        {
            _inner.Warn(message);
        }
    }

    public void Error(string message) => _inner.Error(message);

    private static LogLevel Parse(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "error" => LogLevel.Error,
            "info" => LogLevel.Info,
            "debug" => LogLevel.Debug,
            _ => LogLevel.Warn,
        };

    private enum LogLevel
    {
        Error = 0,
        Warn = 1,
        Info = 2,
        Debug = 3,
    }
}
