// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Recovery;

namespace Core.Cli.Recovery;

internal static class AutomaticRecoveryPolicy
{
    internal static bool ShouldStart(AgentOptions options, bool inAgentMode, bool inputRedirected, bool outputRedirected)
    {
        var trigger = options.Recovery?.Trigger?.ToLowerInvariant();
        if (trigger is not ("off" or "manual" or "auto"))
            throw new ArgumentException("agent.recovery.trigger must be off, manual, or auto.", nameof(options));
        if (trigger != "auto" || inAgentMode)
            return false;

        var settings = RecoverySessionSettings.Resolve(options, RecoveryTrigger.Automatic);
        return !settings.IsInteractive || (!inputRedirected && !outputRedirected);
    }
}
