// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Core.Agent.Telemetry;

/// <summary>
/// Reads the W3C traceparent the CLI puts on this process's environment block so the
/// agent's spans join the CLI.Activity.AiRecovery trace instead of starting an unrelated
/// one (Azure/CLIPS#602).
/// </summary>
/// <remarks>
/// The variable is namespaced rather than the bare TRACEPARENT: a surrounding CI system
/// commonly sets TRACEPARENT, and inheriting it would silently reparent the agent under
/// a pipeline's trace.
/// </remarks>
internal static class RecoveryTraceContext
{
    /// <summary>Must match <c>Core.Cli.Program.RecoveryTraceparentEnvVar</c>.</summary>
    internal const string EnvVar = "AZCLIPS_RECOVERY_TRACEPARENT";

    internal static ActivityContext FromEnvironment() =>
        Parse(Environment.GetEnvironmentVariable(EnvVar));

    /// <summary>
    /// Returns <c>default(ActivityContext)</c> when the value is absent or malformed, which
    /// makes the caller fall back to ambient parenting — the agent then starts its own root
    /// and only the link to the recovery trace is lost.
    /// </summary>
    internal static ActivityContext Parse(string? traceparent) =>
        !string.IsNullOrWhiteSpace(traceparent)
        && ActivityContext.TryParse(traceparent, traceState: null, out var context)
            ? context
            : default;
}
