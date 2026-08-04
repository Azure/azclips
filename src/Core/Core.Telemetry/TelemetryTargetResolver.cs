// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Core.Telemetry;

/// <summary>
/// Resolves the effective <see cref="TelemetryTarget"/>. The default is chosen at
/// compile time (Debug → Local, Release → Remote) and can be overridden only by a
/// direct read of the <c>AZCLIPS_TELEMETRY_TARGET</c> environment variable. This
/// deliberately bypasses the surfaced configuration pipeline so the switch never
/// appears in <c>config get/list/set</c> or tab-completion.
/// </summary>
internal static class TelemetryTargetResolver
{
    internal const string TargetEnvironmentVariable = "AZCLIPS_TELEMETRY_TARGET";

    /// <summary>Resolves the target using the compile-time default and the process environment.</summary>
    internal static TelemetryTarget Resolve() =>
        Resolve(CompileTimeDefault, Environment.GetEnvironmentVariable);

    internal static TelemetryTarget Resolve(TelemetryTarget compileDefault, Func<string, string?> environmentReader)
    {
        var raw = environmentReader(TargetEnvironmentVariable)?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            return compileDefault;
        }

        return raw.ToLowerInvariant() switch
        {
            "local" => TelemetryTarget.Local,
            "remote" => TelemetryTarget.Remote,
            _ => compileDefault,
        };
    }

    private static TelemetryTarget CompileTimeDefault =>
#if DEBUG
        TelemetryTarget.Local;
#else
        TelemetryTarget.Remote;
#endif
}
