// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Recovery;

/// <summary>
/// Acquires the history lock before calling configuration operations. Callbacks must read fresh
/// state; the commit callback acquires the configuration lock, never the other way around.
/// </summary>
public interface IRecoveryConfigurationCoordinator
{
    Task CommitTriggerChangeAsync(
        string nextTrigger,
        Func<CancellationToken, Task<string>> readStoredTrigger,
        Func<CancellationToken, Task> commit,
        CancellationToken cancellationToken = default);
}
