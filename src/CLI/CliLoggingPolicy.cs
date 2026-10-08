// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Cli;

internal static class CliLoggingPolicy
{
    public static LogLevel ResolveEffectiveLevel(IConfigurationService configurationService, bool debugOverride)
        => ResolveEffectiveLevel(configurationService.GetConfiguration<CoreOptions>(), debugOverride);

    public static LogLevel ResolveEffectiveLevel(CoreOptions options, bool debugOverride)
    {
        if (debugOverride)
        {
            return LogLevel.Debug;
        }

        return LoggingPolicy.Parse(options.Logging.Level);
    }
}