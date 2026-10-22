// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security;
using System.Security.Cryptography;
using Core.Abstractions.Recovery;

namespace Core.Runtime.Recovery;

/// <summary>
/// Atomic authenticated snapshots with a durable ID allocator, serialized by one writer lock.
/// When OS-protected key storage is unavailable, only the latest failure is kept unencrypted
/// in a current-user-only file; the next encrypted write moves it into the snapshot.
/// </summary>
public sealed class EncryptedFailureHistoryStore : IFailureHistoryStore
{
    public const string HistoryFileName = FailureHistoryFiles.HistoryFileName;
    public const string ControlFileName = FailureHistoryFiles.ControlFileName;
    public const string LockFileName = FailureHistoryFiles.LockFileName;
    public const string PlaintextHistoryFileName = FailureHistoryFiles.PlaintextHistoryFileName;

    private readonly IFailureHistoryKeyStore _keyStore;
    private readonly FailureHistoryStateStore _state;

    public EncryptedFailureHistoryStore(IFailureHistoryKeyStore keyStore, string? historyDirectory = null)
        : this(keyStore, new FailureHistoryStateStore(new FailureHistoryFiles(historyDirectory))) { }

    internal EncryptedFailureHistoryStore(IFailureHistoryKeyStore keyStore, FailureHistoryStateStore state)
    {
        _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
        _state = state;
    }

    public async Task<FailureHistoryEntry> SaveAsync(FailureRecord record, int maxEntries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEntries);
        cancellationToken.ThrowIfCancellationRequested();
        record = FailureHistoryCodec.FreezePendingRecord(record);
        try
        {
            await using var writerLock = await _state.Files.AcquireWriterLockAsync(cancellationToken).ConfigureAwait(false);
            // A writer killed mid-write can leave plaintext diagnostics in staging; remove them before saving.
            _state.Files.DeleteOrphanedHistoryStaging(cancellationToken);
            var state = await _state.ReadAsync(cancellationToken).ConfigureAwait(false) ?? new();
            var snapshot = await ReadSnapshotAsync(includeCiphertext: true, cancellationToken).ConfigureAwait(false);
            ValidateSnapshotState(snapshot, state);
            var plaintext = await ReadPlaintextAsync(state, cancellationToken).ConfigureAwait(false);

            byte[] key;
            try { key = await GetKeyAsync(createIfMissing: snapshot is null, cancellationToken).ConfigureAwait(false); }
            catch (FailureHistoryException exception) when (exception.Code == KeyUnavailableCode)
            {
                return await SavePlaintextAsync(record, state, cancellationToken).ConfigureAwait(false);
            }
            try
            {
                IReadOnlyList<FailureRecord> previous = snapshot is null ? [] : FailureHistoryCodec.DecryptRecords(snapshot, key);
                if (plaintext is not null)
                    previous = [plaintext, .. previous];
                // Reserve before replacing the snapshot. A crash may leave a gap, never a reused ID.
                await _state.ReserveSequenceAsync(state, cancellationToken).ConfigureAwait(false);
                record = Commit(record, state.NextSequence);
                var records = new List<FailureRecord>(Math.Min(maxEntries, previous.Count + 1)) { record };
                records.AddRange(previous.Take(maxEntries - 1));
                await FailureHistoryCodec.WriteAsync(_state.Files, records, key, cancellationToken).ConfigureAwait(false);
                if (plaintext is not null)
                    File.Delete(_state.Files.PlaintextHistoryPath);
                return new FailureHistoryEntry(record.Id, record.OccurredAtUtc);
            }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
        catch (Exception exception) when (FailureHistoryFiles.IsStorageException(exception)) { throw StorageUnavailable(); }
    }

    public async Task<IReadOnlyList<FailureRecord>> ReadRecordsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var state = await _state.ReadAsync(cancellationToken).ConfigureAwait(false);
            var snapshot = await ReadSnapshotAsync(includeCiphertext: true, cancellationToken).ConfigureAwait(false);
            ValidateSnapshotState(snapshot, state);
            var plaintext = await ReadPlaintextAsync(state, cancellationToken).ConfigureAwait(false);
            if (snapshot is null)
                return plaintext is null ? [] : [plaintext];
            byte[] key;
            try { key = await GetKeyAsync(createIfMissing: false, cancellationToken).ConfigureAwait(false); }
            catch (FailureHistoryException exception) when (exception.Code == KeyUnavailableCode && plaintext is not null)
            {
                return [plaintext];
            }
            try
            {
                var records = FailureHistoryCodec.DecryptRecords(snapshot, key);
                return plaintext is null ? records : [plaintext, .. records];
            }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
        catch (Exception exception) when (FailureHistoryFiles.IsStorageException(exception)) { throw StorageUnavailable(); }
    }

    public async Task<IReadOnlyList<FailureHistoryEntry>> ReadIndexAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var state = await _state.ReadAsync(cancellationToken).ConfigureAwait(false);
            var snapshot = await ReadSnapshotAsync(includeCiphertext: false, cancellationToken).ConfigureAwait(false);
            ValidateSnapshotState(snapshot, state);
            var plaintext = await ReadPlaintextAsync(state, cancellationToken).ConfigureAwait(false);
            // Without key access only structure is checked. Selected records authenticate the same
            // index as AES-GCM AAD before any diagnostic content can be used.
            IReadOnlyList<FailureHistoryEntry> entries = snapshot?.Entries ?? [];
            return plaintext is null
                ? entries
                : [new FailureHistoryEntry(plaintext.Id, plaintext.OccurredAtUtc, Unencrypted: true), .. entries];
        }
        catch (Exception exception) when (FailureHistoryFiles.IsStorageException(exception)) { throw StorageUnavailable(); }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var writerLock = await _state.Files.AcquireWriterLockAsync(cancellationToken).ConfigureAwait(false);
            _state.Files.DeleteControlledData(cancellationToken);
        }
        catch (Exception exception) when (FailureHistoryFiles.IsStorageException(exception))
        {
            throw FailureHistoryStateStore.ClearIncomplete();
        }
    }

    private static FailureRecord Commit(FailureRecord record, ulong sequence) => record with
    {
        Sequence = sequence,
        Id = FailureId.Format(record.TerminalSessionId, record.OccurredAtUtc, sequence),
    };

    private static void ValidateSnapshotState(FailureHistorySnapshot? snapshot, FailureHistoryControl? state)
    {
        if (snapshot is null)
            return;
        if (state is null ||
            snapshot.Entries.Any(entry => !FailureId.TryParse(entry.Id, out var id) || id.Sequence >= state.NextSequence))
            throw new FailureHistoryException("failure_history_state_mismatch", "Failure history does not match its durable allocator state. No history was replaced.");
    }

    private async Task<FailureHistoryEntry> SavePlaintextAsync(
        FailureRecord record, FailureHistoryControl state, CancellationToken cancellationToken)
    {
        await _state.ReserveSequenceAsync(state, cancellationToken).ConfigureAwait(false);
        record = Commit(record, state.NextSequence);
        await FailureHistoryCodec.WritePlaintextAsync(_state.Files, record, cancellationToken).ConfigureAwait(false);
        return new FailureHistoryEntry(record.Id, record.OccurredAtUtc, Unencrypted: true);
    }

    private async Task<FailureRecord?> ReadPlaintextAsync(FailureHistoryControl? state, CancellationToken cancellationToken)
    {
        var record = await FailureHistoryCodec.ReadPlaintextAsync(_state.Files, cancellationToken).ConfigureAwait(false);
        if (record is not null &&
            (state is null || record.Sequence >= state.NextSequence))
            throw FailureHistoryCodec.PlaintextCorrupt();
        return record;
    }

    private async Task<FailureHistorySnapshot?> ReadSnapshotAsync(bool includeCiphertext, CancellationToken cancellationToken)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(_state.Files.HistoryPath, new FileStreamOptions
            {
                Mode = FileMode.Open, Access = FileAccess.Read, Share = FileShare.Read | FileShare.Delete,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException) { return null; }
        await using (stream.ConfigureAwait(false))
            return await FailureHistorySnapshot.ReadAsync(stream, includeCiphertext, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> GetKeyAsync(bool createIfMissing, CancellationToken cancellationToken)
    {
        byte[]? key = null;
        try
        {
            key = await _keyStore.GetKeyAsync(createIfMissing, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (key is not { Length: 32 })
                throw new FailureHistoryException("failure_history_key_invalid", "The protected failure-history key is invalid.");
            var owned = key;
            key = null;
            return owned;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or SecurityException or CryptographicException or FormatException or
            PlatformNotSupportedException or DllNotFoundException or EntryPointNotFoundException)
        {
            throw new FailureHistoryException("failure_history_key_unavailable", "The protected failure-history key is unavailable.");
        }
        finally
        {
            if (key is not null)
                CryptographicOperations.ZeroMemory(key);
        }
    }

    private const string KeyUnavailableCode = "failure_history_key_unavailable";

    private static FailureHistoryException StorageUnavailable() =>
        new("failure_history_io", "Failure history could not be accessed or saved. Check its directory permissions and available disk space.");
}
