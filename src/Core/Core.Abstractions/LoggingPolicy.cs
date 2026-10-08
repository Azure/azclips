// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Shared baseline thresholds for core.logging.level, independent of host overrides and output routing.
/// </summary>
public static class LoggingPolicy
{
    public static LogLevel Parse(string? configuredLevel) => configuredLevel?.Trim().ToLowerInvariant() switch
    {
        "error" => LogLevel.Error,
        "warn" => LogLevel.Warn,
        "info" => LogLevel.Info,
        "debug" => LogLevel.Debug,
        _ => LogLevel.Warn,
    };

    public static bool IsEnabled(LogLevel configuredLevel, LogSeverity severity) => severity switch
    {
        LogSeverity.Error => true,
        LogSeverity.Warn => configuredLevel >= LogLevel.Warn,
        LogSeverity.Info => configuredLevel >= LogLevel.Info,
        LogSeverity.Debug or LogSeverity.Verbose => configuredLevel >= LogLevel.Debug,
        _ => false,
    };
}
