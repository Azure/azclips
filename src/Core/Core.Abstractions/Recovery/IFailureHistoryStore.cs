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

    /// <summary>
    /// Returns the record with <paramref name="id"/>, or the newest record when <paramref name="id"/> is null,
    /// only when it is stored unencrypted; it needs no key.
    /// </summary>
    Task<FailureRecord?> ReadPlainAsync(string? id, CancellationToken cancellationToken = default) => Task.FromResult<FailureRecord?>(null);

    /// <summary>Reads only record IDs and times, without accessing the key store.</summary>
    Task<IReadOnlyList<FailureHistoryEntry>> ReadIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>Deletes the history, its temporary files, and the key file, without decrypting.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
