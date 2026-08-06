// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Cli;

internal enum CliLogLevel
{
    Error = 0,
    Warn = 1,
    Info = 2,
    Debug = 3,
}

internal enum CliLogSeverity
{
    Error = 0,
    Warn = 1,
    Info = 2,
    Debug = 3,
    Verbose = 4,
}

internal static class CliLoggingPolicy
{
    public static CliLogLevel ResolveEffectiveLevel(IConfigurationService configurationService, bool debugOverride)
        => ResolveEffectiveLevel(configurationService.GetConfiguration<CoreOptions>(), debugOverride);

    public static CliLogLevel ResolveEffectiveLevel(CoreOptions options, bool debugOverride)
    {
        if (debugOverride)
        {
            return CliLogLevel.Debug;
        }

        return Parse(options.Logging.Level);
    }

    public static CliLogLevel Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CliLogLevel.Warn;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "error" => CliLogLevel.Error,
            "warn" => CliLogLevel.Warn,
            "info" => CliLogLevel.Info,
            "debug" => CliLogLevel.Debug,
            _ => CliLogLevel.Warn,
        };
    }

    public static bool ShouldEmit(CliLogLevel configuredLevel, CliLogSeverity messageSeverity)
    {
        return messageSeverity switch
        {
            CliLogSeverity.Error => true,
            CliLogSeverity.Warn => configuredLevel >= CliLogLevel.Warn,
            CliLogSeverity.Info => configuredLevel >= CliLogLevel.Info,
            CliLogSeverity.Debug => configuredLevel >= CliLogLevel.Debug,
            CliLogSeverity.Verbose => configuredLevel >= CliLogLevel.Debug,
            _ => false,
        };
    }
}