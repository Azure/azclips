// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Recovery;

public interface IFailureHistoryStore
{
    /// <summary>
    /// Commits a pending record (empty ID, original shell PID) and returns its store-allocated ID.
    /// Keeps at most <paramref name="maxEntries"/> records, newest first.
    /// </summary>
    Task<FailureHistoryEntry> SaveAsync(FailureRecord record, int maxEntries, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FailureRecord>> ReadRecordsAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads only the non-sensitive selection index, without accessing the key store.</summary>
    Task<IReadOnlyList<FailureHistoryEntry>> ReadIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears history and key files without decrypting or accessing native key stores.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
