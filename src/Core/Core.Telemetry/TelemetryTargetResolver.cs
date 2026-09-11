// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Core.Telemetry;

/// <summary>
/// Resolves the effective set of <see cref="TelemetryTarget"/> values. The default is chosen
/// at compile time (Debug → Local and Remote, Release → Remote). In Debug it can be overridden
/// with <c>AZCLIPS_TELEMETRY_TARGET</c>, which accepts <c>local</c>, <c>remote</c>, or
/// <c>both</c>; Release does not read the environment. The variable bypasses the surfaced
/// configuration pipeline, so it never appears in <c>config get/list/set</c> or tab-completion.
///
/// The resolved set is never empty. Telemetry is disabled with <c>telemetry.enabled=false</c>.
/// </summary>
internal static class TelemetryTargetResolver
{
    internal const string TargetEnvironmentVariable = "AZCLIPS_TELEMETRY_TARGET";

    /// <summary>Resolves the target set for this build.</summary>
    internal static IReadOnlyList<TelemetryTarget> Resolve() =>
        Resolve(CompileTimeDefault, EnvironmentReader);

    internal static IReadOnlyList<TelemetryTarget> Resolve(
        IReadOnlyList<TelemetryTarget> compileDefault,
        Func<string, string?> environmentReader)
    {
        var raw = environmentReader(TargetEnvironmentVariable)?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            return compileDefault;
        }

        return raw.ToLowerInvariant() switch
        {
            "local" => [TelemetryTarget.Local],
            "remote" => [TelemetryTarget.Remote],
            "both" => [TelemetryTarget.Local, TelemetryTarget.Remote],
            _ => compileDefault,
        };
    }

    /// <summary>Reads the process environment in Debug; always returns null in Release.</summary>
    internal static Func<string, string?> EnvironmentReader =>
#if DEBUG
        Environment.GetEnvironmentVariable;
#else
        _ => null;
#endif

    /// <summary>The target set compiled into this build.</summary>
    internal static IReadOnlyList<TelemetryTarget> CompileTimeDefault =>
#if DEBUG
        [TelemetryTarget.Local, TelemetryTarget.Remote];
#else
        [TelemetryTarget.Remote];
#endif
}
