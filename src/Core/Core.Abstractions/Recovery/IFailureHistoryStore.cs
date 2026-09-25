// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Recovery;

public interface IFailureHistoryStore
{
    /// <summary>
    /// Commits a pending record (empty ID, original shell PID, starting generation) and returns
    /// its store-allocated ID. The positive limit hint is retained for callers; fresh effective
    /// configuration under the writer lock determines retention.
    /// </summary>
    Task<FailureHistoryEntry> SaveAsync(FailureRecord record, int maxEntries, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FailureRecord>> ReadRecordsAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads only the non-sensitive selection index, without accessing the key store.</summary>
    Task<IReadOnlyList<FailureHistoryEntry>> ReadIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads the non-sensitive fencing generation without creating state or accessing keys.</summary>
    Task<ulong> ReadGenerationAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears controlled history/key files without decrypting or accessing native key stores.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>Applies fresh off/pending-clear policy at an ordinary, non-read-only CLI lifecycle.</summary>
    Task ApplyPolicyAsync(CancellationToken cancellationToken = default);
}
