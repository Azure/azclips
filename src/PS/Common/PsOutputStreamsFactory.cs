using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Cmdlets.Common;

internal enum PsLogLevel
{
    Error = 0,
    Warn = 1,
    Info = 2,
    Debug = 3,
}

internal enum PsLogSeverity
{
    Error = 0,
    Warn = 1,
    Info = 2,
    Debug = 3,
    Verbose = 4,
}

internal static class PsOutputStreamsFactory
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
                if (ShouldEmit(level, PsLogSeverity.Verbose))
                {
                    cmdlet.WriteVerbose(value?.ToString() ?? string.Empty);
                }

                return Task.CompletedTask;
            },
            WriteDebug: (value, ct) =>
            {
                if (ShouldEmit(level, PsLogSeverity.Debug))
                {
                    cmdlet.WriteDebug(value?.ToString() ?? string.Empty);
                }

                return Task.CompletedTask;
            },
            WriteWarning: (value, ct) =>
            {
                if (ShouldEmit(level, PsLogSeverity.Warn))
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
                if (ShouldEmit(level, PsLogSeverity.Info))
                {
                    cmdlet.WriteInformation(new InformationRecord(value, null));
                }

                return Task.CompletedTask;
            });
    }

    private static PsLogLevel ResolveEffectiveLevel()
    {
        var configurationService = CirrusServiceProvider.GetRequiredService<IConfigurationService>();
        var configured = configurationService.GetConfiguration<CoreOptions>().Logging.Level;
        return Parse(configured);
    }

    private static PsLogLevel Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return PsLogLevel.Info;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "error" => PsLogLevel.Error,
            "warn" => PsLogLevel.Warn,
            "info" => PsLogLevel.Info,
            "debug" => PsLogLevel.Debug,
            _ => PsLogLevel.Info,
        };
    }

    private static bool ShouldEmit(PsLogLevel configuredLevel, PsLogSeverity messageSeverity)
    {
        return messageSeverity switch
        {
            PsLogSeverity.Error => true,
            PsLogSeverity.Warn => configuredLevel >= PsLogLevel.Warn,
            PsLogSeverity.Info => configuredLevel >= PsLogLevel.Info,
            PsLogSeverity.Debug => configuredLevel >= PsLogLevel.Debug,
            PsLogSeverity.Verbose => configuredLevel >= PsLogLevel.Debug,
            _ => false,
        };
    }
}