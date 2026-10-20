// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Recovery;

namespace Core.Cli.Recovery;

internal static class AutomaticRecoveryPolicy
{
    internal static bool ShouldStart(AgentOptions options, bool inAgentMode, bool inputRedirected, bool outputRedirected)
    {
        if (!options.IsRecoveryAutomatic || inAgentMode)
            return false;

        var settings = RecoverySessionSettings.Resolve(options, RecoveryTrigger.Automatic);
        return !settings.IsInteractive || (!inputRedirected && !outputRedirected);
    }
}
