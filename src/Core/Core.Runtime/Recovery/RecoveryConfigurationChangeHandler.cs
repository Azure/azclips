// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Recovery;
using Core.Abstractions.Runtime.Handler;

namespace Core.Runtime.Recovery;

/// <summary>
/// Clears failure history when agent.recovery.trigger is set to off or deleted (its default is off).
/// </summary>
public sealed class RecoveryConfigurationChangeHandler(IFailureHistoryStore store) : IConfigurationChangeHandler
{
    public const string TriggerKey = "agent.recovery.trigger";

    public async Task OnChangedAsync(string key, object? value, CancellationToken cancellationToken)
    {
        if (value is not null && !string.Equals(Convert.ToString(value), "off", StringComparison.OrdinalIgnoreCase))
            return;
        try
        {
            await store.ClearAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (FailureHistoryException)
        {
            throw new InvalidOperationException(
                $"Failure history could not be cleared. Run 'az config set --key {TriggerKey} --value off' again.");
        }
    }
}
