// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Recovery;

public interface IFailureHistoryStore
{
    Task<FailureHistoryEntry> SaveAsync(FailureRecord record, int maxEntries, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FailureRecord>> ReadRecordsAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads only the non-sensitive selection index, without accessing the key store.</summary>
    Task<IReadOnlyList<FailureHistoryEntry>> ReadIndexAsync(CancellationToken cancellationToken = default);
}
