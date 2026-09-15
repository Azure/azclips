// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;

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
    /// <summary>Must match <c>Core.Cli.Program.RecoveryTraceparentEnvVar</c>.</summary>
    internal const string EnvVar = TelemetryHierarchy.RecoveryTraceparentEnvVar;

    internal static Activity? StartSession(ActivitySource source) =>
        StartSession(source, Environment.GetEnvironmentVariable(EnvVar),
            Environment.GetEnvironmentVariable(TelemetryHierarchy.RecoveryHierarchyEnvVar));

    internal static Activity? StartSession(ActivitySource source, string? traceparent, string? hierarchy)
    {
        var context = Parse(traceparent);
        TelemetryHierarchy.TryReadRecoveryHandoff(context, hierarchy, out var parent);
        if ((!string.IsNullOrEmpty(traceparent) && context == default)
            || (!string.IsNullOrEmpty(hierarchy) && parent is null))
        {
            Console.Error.WriteLine("Ignoring invalid recovery telemetry handoff; source lineage is unavailable.");
        }

        return TelemetryHierarchy.StartActivity(
            source, "Agent.Session", ActivityKind.Server, context, parent);
    }

    /// <summary>
    /// Returns <c>default(ActivityContext)</c> when the value is absent or malformed. The
    /// session producer explicitly starts its own root and does not inherit ambient work.
    /// </summary>
    internal static ActivityContext Parse(string? traceparent) =>
        !string.IsNullOrWhiteSpace(traceparent)
        && ActivityContext.TryParse(traceparent, traceState: null, isRemote: true, out var context)
            ? context
            : default;
}
