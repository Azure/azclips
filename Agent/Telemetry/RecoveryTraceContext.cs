// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Core.Agent.Telemetry;

/// <summary>
/// Reads the W3C traceparent the CLI sets on this process's environment block, so agent
/// spans join the CLI's recovery trace.
/// </summary>
/// <remarks>
/// The variable is namespaced rather than the bare TRACEPARENT, which CI systems commonly
/// set and would reparent the agent under a pipeline's trace.
/// </remarks>
internal static class RecoveryTraceContext
{
    /// <summary>Must match <c>Core.Runtime.Recovery.AgentLauncher.RecoveryTraceparentEnvVar</c>.</summary>
    internal const string EnvVar = "AZCLIPS_RECOVERY_TRACEPARENT";

    internal static ActivityContext FromEnvironment() =>
        Parse(Environment.GetEnvironmentVariable(EnvVar));

    /// <summary>
    /// Returns <c>default(ActivityContext)</c> when the value is absent or malformed. The
    /// caller then starts its own root and only the link to the CLI's trace is lost.
    /// </summary>
    internal static ActivityContext Parse(string? traceparent) =>
        !string.IsNullOrWhiteSpace(traceparent)
        && ActivityContext.TryParse(traceparent, traceState: null, out var context)
            ? context
            : default;
}
