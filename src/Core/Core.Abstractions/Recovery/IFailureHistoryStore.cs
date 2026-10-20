// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Recovery;

public interface IFailureHistoryStore
{
    /// <summary>
    /// Commits a pending record (empty ID, original shell PID, generation from BeginCaptureAsync)
    /// and returns its store-allocated ID. The positive limit hint is retained for callers; fresh effective
    /// configuration under the writer lock determines retention.
    /// </summary>
    Task<FailureHistoryEntry> SaveAsync(FailureRecord record, int maxEntries, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FailureRecord>> ReadRecordsAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads only the non-sensitive selection index, without accessing the key store.</summary>
    Task<IReadOnlyList<FailureHistoryEntry>> ReadIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a capture against fresh effective policy and the reset generation, without key
    /// access. Returns null when off is observed; otherwise returns the generation the pending
    /// record must retain. Call before reading capture options or preparing a deferred writer.
    /// </summary>
    Task<ulong?> BeginCaptureAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inspects the non-sensitive fencing generation without creating state or accessing keys.
    /// This read-only operation does not register a capture; writers must use BeginCaptureAsync.
    /// </summary>
    Task<ulong> ReadGenerationAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears controlled history/key files without decrypting or accessing native key stores.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
