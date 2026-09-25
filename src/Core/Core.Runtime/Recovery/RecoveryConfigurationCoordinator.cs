// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Recovery;

namespace Core.Runtime.Recovery;

public sealed class RecoveryConfigurationCoordinator : IRecoveryConfigurationCoordinator
{
    private readonly FailureHistoryStateStore _state;

    public RecoveryConfigurationCoordinator(string? historyDirectory = null)
        : this(new FailureHistoryStateStore(new FailureHistoryFiles(historyDirectory))) { }

    internal RecoveryConfigurationCoordinator(FailureHistoryStateStore state) => _state = state;

    public async Task CommitTriggerChangeAsync(
        string nextTrigger,
        Func<CancellationToken, Task<string>> readStoredTrigger,
        Func<CancellationToken, Task> commit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(readStoredTrigger);
        ArgumentNullException.ThrowIfNull(commit);
        var next = new RecoveryOptions { Trigger = nextTrigger };
        next.Validate();

        await using var writerLock = await _state.Files.AcquireWriterLockAsync(cancellationToken).ConfigureAwait(false);
        if (!next.IsEnabled)
        {
            await commit(cancellationToken).ConfigureAwait(false);
            try { await _state.ClearUnderLockAsync(cancellationToken).ConfigureAwait(false); }
            catch (Exception exception) when (IsClearFailure(exception))
            {
                throw FailureHistoryStateStore.ClearIncomplete(recoveryOff: true, exception);
            }
            return;
        }

        var previous = await readStoredTrigger(cancellationToken).ConfigureAwait(false);
        var state = await _state.ReadAsync(cancellationToken).ConfigureAwait(false);
        // Even when an earlier off commit could not write its pending marker, persisted off is
        // sufficient to fence pre-off writers before allowing a re-enable.
        if (string.Equals(previous, "off", StringComparison.OrdinalIgnoreCase) || state?.ClearPending == true)
        {
            try { await _state.ClearUnderLockAsync(cancellationToken).ConfigureAwait(false); }
            catch (Exception exception) when (IsClearFailure(exception))
            {
                throw FailureHistoryStateStore.ClearIncomplete(
                    recoveryOff: string.Equals(previous, "off", StringComparison.OrdinalIgnoreCase), exception);
            }
        }

        await commit(cancellationToken).ConfigureAwait(false);
    }

    private static bool IsClearFailure(Exception exception) =>
        exception is FailureHistoryException or OperationCanceledException || FailureHistoryFiles.IsStorageException(exception);
}
