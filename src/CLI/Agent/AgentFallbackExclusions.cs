// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli.Agent;

/// <summary>
/// Decides whether a failing command's exception should be excluded from the
/// agent error-recovery fallback (i.e. surfaced directly instead of spawning the
/// agent).
///
/// The exclusion set is intentionally EMPTY right now: every exception still
/// reaches the agent, so behavior is unchanged. This is the skeleton for a future
/// data-driven decision — the telemetry side (see AgentFallbackTrace) records the
/// exceptions that trigger fallback so we can later determine which types are
/// well-understood enough that an LLM round-trip adds nothing, and add them here.
/// </summary>
internal static class AgentFallbackExclusions
{
    // Exception types that should NOT trigger the agent. Empty by design until
    // trace data justifies each addition (match by exact type or any base type).
    private static readonly IReadOnlyList<Type> s_excludedTypes = Array.Empty<Type>();

    /// <summary>
    /// True if <paramref name="ex"/> should bypass the agent fallback. Always
    /// false while the exclusion set is empty.
    /// </summary>
    public static bool IsExcluded(Exception ex)
    {
        if (ex is null) return false;
        var actual = ex.GetType();
        foreach (var excluded in s_excludedTypes)
        {
            if (excluded.IsAssignableFrom(actual)) return true;
        }
        return false;
    }
}
