// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security;
using System.Security.Cryptography;
using Core.Abstractions;
using Core.Abstractions.Recovery;

namespace Core.Runtime.Recovery;

/// <summary>Atomic authenticated snapshots with an independent durable allocator and reset fence.</summary>
public sealed class EncryptedFailureHistoryStore : IFailureHistoryStore
{
    public const string HistoryFileName = FailureHistoryFiles.HistoryFileName;
    public const string LegacyHistoryFileName = FailureHistoryFiles.LegacyHistoryFileName;
    public const string ControlFileName = FailureHistoryFiles.ControlFileName;
    public const string LockFileName = FailureHistoryFiles.LockFileName;

    private readonly IFailureHistoryKeyStore _keyStore;
    private readonly IConfigurationService _configuration;
    private readonly FailureHistoryStateStore _state;

    public EncryptedFailureHistoryStore(IFailureHistoryKeyStore keyStore, IConfigurationService configuration, string? historyDirectory = null)
        : this(keyStore, configuration, new FailureHistoryStateStore(new FailureHistoryFiles(historyDirectory))) { }

    internal EncryptedFailureHistoryStore(IFailureHistoryKeyStore keyStore, IConfigurationService configuration, FailureHistoryStateStore state)
    {
        _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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
            var state = await _state.ReadAsync(cancellationToken).ConfigureAwait(false);
            ValidateWritePolicy(record, state ?? new());
            ThrowIfLegacyHistory();
            var snapshot = await ReadSnapshotAsync(includeCiphertext: true, cancellationToken).ConfigureAwait(false);
            ValidateSnapshotState(snapshot, state);
            if (state is null)
            {
                state = new();
                await _state.WriteAsync(state, cancellationToken).ConfigureAwait(false);
            }

            var key = await GetKeyAsync(state.Generation, createIfMissing: snapshot is null, cancellationToken).ConfigureAwait(false);
            try
            {
                var previous = snapshot is null ? [] : FailureHistoryCodec.DecryptRecords(snapshot, key);
                ValidateWritePolicy(record, state);
                // Reserve before replacing the snapshot. A crash may leave a gap, never a reused ID.
                await _state.ReserveSequenceAsync(state, cancellationToken).ConfigureAwait(false);
                record = record with
                {
                    Sequence = state.NextSequence,
                    Id = FailureId.Format(record.TerminalSessionId, record.OccurredAtUtc, state.NextSequence),
                };
                var policy = ValidateWritePolicy(record, state);
                var records = new List<FailureRecord>(Math.Min(policy.HistorySize, previous.Count + 1)) { record };
                records.AddRange(previous.Take(policy.HistorySize - 1));
                await FailureHistoryCodec.WriteAsync(_state.Files, records, state.Generation, key, cancellationToken).ConfigureAwait(false);
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
            var snapshot = await ReadForViewAsync(includeCiphertext: true, cancellationToken).ConfigureAwait(false);
            if (snapshot is null)
                return [];
            var key = await GetKeyAsync(snapshot.Generation, createIfMissing: false, cancellationToken).ConfigureAwait(false);
            try
            {
                var records = FailureHistoryCodec.DecryptRecords(snapshot, key);
                await ValidateViewStateAsync(snapshot, cancellationToken).ConfigureAwait(false);
                return records;
            }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
        catch (Exception exception) when (FailureHistoryFiles.IsStorageException(exception)) { throw StorageUnavailable(); }
    }

    public async Task<IReadOnlyList<FailureHistoryEntry>> ReadIndexAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await ReadForViewAsync(includeCiphertext: false, cancellationToken).ConfigureAwait(false);
            // Without key access only structure is checked. Selected records authenticate the same
            // index and generation as AES-GCM AAD before any diagnostic content can be used.
            return snapshot?.Entries ?? [];
        }
        catch (Exception exception) when (FailureHistoryFiles.IsStorageException(exception)) { throw StorageUnavailable(); }
    }

    public async Task<ulong> ReadGenerationAsync(CancellationToken cancellationToken = default)
    {
        try { return (await _state.ReadAsync(cancellationToken).ConfigureAwait(false))?.Generation ?? 0; }
        catch (Exception exception) when (FailureHistoryFiles.IsStorageException(exception)) { throw StorageUnavailable(); }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var writerLock = await _state.Files.AcquireWriterLockAsync(cancellationToken).ConfigureAwait(false);
            await _state.ClearUnderLockAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (FailureHistoryFiles.IsStorageException(exception))
        {
            throw FailureHistoryStateStore.ClearIncomplete(recoveryOff: false);
        }
    }

    public async Task ApplyPolicyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var state = await _state.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (!NeedsPolicyCleanup(ReadPolicy(), state))
                return;
            await using var writerLock = await _state.Files.AcquireWriterLockAsync(cancellationToken).ConfigureAwait(false);
            state = await _state.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (NeedsPolicyCleanup(ReadPolicy(), state))
                await _state.ClearUnderLockAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (FailureHistoryFiles.IsStorageException(exception))
        {
            throw FailureHistoryStateStore.ClearIncomplete(recoveryOff: false);
        }
    }

    private bool NeedsPolicyCleanup(RecoveryOptions policy, FailureHistoryControl? state) =>
        // Empty history can still have a pre-off capture in flight. Observing off must fence it,
        // including after an earlier reset and before an externally edited policy re-enables.
        state?.ClearPending == true || !policy.IsEnabled;

    private RecoveryOptions ReadPolicy()
    {
        var recovery = _configuration.GetConfiguration<AgentOptions>().Recovery;
        recovery.Validate();
        return recovery;
    }

    private RecoveryOptions ValidateWritePolicy(FailureRecord record, FailureHistoryControl state)
    {
        var policy = ReadPolicy();
        if (!policy.IsEnabled)
            throw new FailureHistoryException("failure_history_disabled", "Recovery is off; failure history was not saved.");
        if (state.ClearPending)
            throw FailureHistoryStateStore.PendingClear();
        if (record.HistoryGeneration != state.Generation)
            throw new FailureHistoryException("failure_history_stale_generation", "Recovery history was reset after this capture started. The stale failure was not saved.");
        return policy;
    }

    private async Task<FailureHistorySnapshot?> ReadForViewAsync(bool includeCiphertext, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await ValidateViewStateAsync(null, cancellationToken).ConfigureAwait(false);
        ThrowIfLegacyHistory();
        var snapshot = await ReadSnapshotAsync(includeCiphertext, cancellationToken).ConfigureAwait(false);
        await ValidateViewStateAsync(snapshot, cancellationToken).ConfigureAwait(false);
        return snapshot;
    }

    private async Task ValidateViewStateAsync(FailureHistorySnapshot? snapshot, CancellationToken cancellationToken)
    {
        var state = await _state.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (state?.ClearPending == true || (!ReadPolicy().IsEnabled && _state.Files.HasControlledData()))
            throw FailureHistoryStateStore.PendingClear();
        ValidateSnapshotState(snapshot, state);
    }

    private static void ValidateSnapshotState(FailureHistorySnapshot? snapshot, FailureHistoryControl? state)
    {
        if (snapshot is null)
            return;
        if (state is null || snapshot.Generation != state.Generation ||
            snapshot.Entries.Any(entry => !FailureId.TryParse(entry.Id, out var id) || id.Sequence >= state.NextSequence))
            throw new FailureHistoryException("failure_history_state_mismatch", "Failure history does not match its durable allocator/generation state. No history was replaced.");
    }

    private void ThrowIfLegacyHistory()
    {
        if (_state.Files.HasLegacyHistory())
            throw FailureHistoryStateStore.IncompatibleDraft();
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

    private async Task<byte[]> GetKeyAsync(ulong generation, bool createIfMissing, CancellationToken cancellationToken)
    {
        byte[]? key = null;
        try
        {
            key = await _keyStore.GetKeyAsync(generation, createIfMissing, cancellationToken).ConfigureAwait(false);
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

    private static FailureHistoryException StorageUnavailable() =>
        new("failure_history_io", "Failure history could not be accessed or saved. Check its directory permissions and available disk space.");
}
