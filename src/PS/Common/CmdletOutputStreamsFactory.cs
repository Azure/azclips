// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Cmdlets.Common;

internal enum LogLevel
{
    Error = 0,
    Warn = 1,
    Info = 2,
    Debug = 3,
}

internal enum LogSeverity
{
    Error = 0,
    Warn = 1,
    Info = 2,
    Debug = 3,
    Verbose = 4,
}

internal static class CmdletOutputStreamsFactory
{
    public static OutputStreams Create(
        PSCmdlet cmdlet,
        string errorId,
        ErrorCategory errorCategory,
        bool enumerateOutput = false)
    {
        var level = ResolveEffectiveLevel();

        return new OutputStreams(
            WriteOutput: (value, ct) =>
            {
                if (enumerateOutput && value is IEnumerable and not string)
                {
                    cmdlet.WriteObject(value, true);
                }
                else
                {
                    cmdlet.WriteObject(value);
                }

                return Task.CompletedTask;
            },
            WriteVerbose: (value, ct) =>
            {
                var message = value?.ToString() ?? string.Empty;
                var verboseOverride = IsOverrideEnabled(cmdlet.MyInvocation.BoundParameters, "Verbose");
                if (verboseOverride)
                {
                    cmdlet.WriteVerbose(message);
                }
                else if (ShouldEmit(level, LogSeverity.Verbose))
                {
                    cmdlet.Host.UI.WriteVerboseLine(message);
                }

                return Task.CompletedTask;
            },
            WriteDebug: (value, ct) =>
            {
                var message = value?.ToString() ?? string.Empty;
                var debugOverride = IsOverrideEnabled(cmdlet.MyInvocation.BoundParameters, "Debug");
                if (debugOverride)
                {
                    cmdlet.WriteDebug(message);
                }
                else if (ShouldEmit(level, LogSeverity.Debug))
                {
                    cmdlet.Host.UI.WriteDebugLine(message);
                }

                return Task.CompletedTask;
            },
            WriteWarning: (value, ct) =>
            {
                if (ShouldEmit(level, LogSeverity.Warn))
                {
                    cmdlet.WriteWarning(value?.ToString() ?? string.Empty);
                }

                return Task.CompletedTask;
            },
            WriteError: (value, ct) =>
            {
                cmdlet.WriteError(new ErrorRecord(
                    new Exception(value?.ToString() ?? string.Empty),
                    errorId,
                    errorCategory,
                    null));
                return Task.CompletedTask;
            },
            WriteInformation: (value, ct) =>
            {
                if (ShouldEmit(level, LogSeverity.Info))
                {
                    cmdlet.WriteInformation(new InformationRecord(value, null));
                }

                return Task.CompletedTask;
            });
    }

    private static LogLevel ResolveEffectiveLevel()
    {
        var configurationService = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();
        var configured = configurationService.GetConfiguration<CoreOptions>().Logging.Level;
        return Parse(configured);
    }

    internal static LogLevel Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return LogLevel.Warn;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "error" => LogLevel.Error,
            "warn" => LogLevel.Warn,
            "info" => LogLevel.Info,
            "debug" => LogLevel.Debug,
            _ => LogLevel.Warn,
        };
    }

    internal static bool ShouldEmit(LogLevel configuredLevel, LogSeverity messageSeverity)
    {
        return messageSeverity switch
        {
            LogSeverity.Error => true,
            LogSeverity.Warn => configuredLevel >= LogLevel.Warn,
            LogSeverity.Info => configuredLevel >= LogLevel.Info,
            LogSeverity.Debug => configuredLevel >= LogLevel.Debug,
            LogSeverity.Verbose => configuredLevel >= LogLevel.Debug,
            _ => false,
        };
    }

    internal static bool IsOverrideEnabled(IDictionary<string, object> boundParameters, string parameterName)
    {
        if (!boundParameters.TryGetValue(parameterName, out var value) || value is null)
        {
            return false;
        }

        return LanguagePrimitives.IsTrue(value);
    }
}
