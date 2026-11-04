// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security;
using System.Security.Cryptography;
using Core.Abstractions;
using Core.Abstractions.Recovery;

namespace Core.Runtime.Recovery;

/// <summary>
/// Failure history in one history.json, replaced atomically under one writer lock.
/// Each record is encrypted with a key from OS-protected storage. While that storage is
/// unavailable, existing encrypted records are kept and at most one record is stored as
/// plain text; the next save with the key available drops it.
/// </summary>
public sealed class EncryptedFailureHistoryStore : IFailureHistoryStore
{
    public const string HistoryFileName = FailureHistoryFiles.HistoryFileName;
    public const string LockFileName = FailureHistoryFiles.LockFileName;

    private const string KeyUnavailableCode = "failure_history_key_unavailable";
    private const string KeyMissingCode = "failure_history_key_missing";

    private readonly IFailureHistoryKeyStore _keyStore;
    private readonly FailureHistoryFiles _files;
    private readonly ILoggingService? _logger;

    public EncryptedFailureHistoryStore(
        IFailureHistoryKeyStore keyStore, string? historyDirectory = null, ILoggingService? logger = null)
        : this(keyStore, new FailureHistoryFiles(historyDirectory), logger) { }

    internal EncryptedFailureHistoryStore(IFailureHistoryKeyStore keyStore, FailureHistoryFiles files, ILoggingService? logger = null)
    {
        _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
        _files = files;
        _logger = logger;
    }

    public async Task<FailureHistoryEntry> SaveAsync(FailureRecord record, int maxEntries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEntries);
        cancellationToken.ThrowIfCancellationRequested();
        record = FailureHistoryCodec.FreezePendingRecord(record);
        Exception? keyError = null;
        var keyReplaced = false;
        try
        {
            await using var writerLock = await _files.AcquireWriterLockAsync(cancellationToken).ConfigureAwait(false);
            var document = FailureHistoryCodec.Parse(await _files.ReadHistoryAsync(cancellationToken).ConfigureAwait(false));
            var sequence = Math.Max(document.NextSequence, document.Records.Select(stored => stored.Sequence + 1).DefaultIfEmpty(0UL).Max());
            if (sequence == ulong.MaxValue)
                throw new FailureHistoryException("failure_history_sequence_exhausted", "The failure-history sequence is exhausted. Set agent.recovery.trigger to off to reset it.");
            record = record with
            {
                Sequence = sequence,
                Id = NewId(record.TerminalSessionId, document),
            };

            (var key, keyError) = await GetKeyAsync(createIfMissing: true, () => keyReplaced = true, cancellationToken).ConfigureAwait(false);
            List<StoredFailureRecord> records;
            try
            {
                records = key is null
                    // Keep encrypted records; the new plain record replaces any earlier one.
                    ? [FailureHistoryCodec.ToPlain(record), .. document.Records.Where(stored => stored.Kind == StoredFailureRecord.Encrypted)]
                    // Drop plain records and encrypted records this key cannot open.
                    : [FailureHistoryCodec.Encrypt(record, key), .. document.Records.Where(stored =>
                        stored.Kind == StoredFailureRecord.Encrypted && FailureHistoryCodec.Open(stored, key) is not null)];
            }
            finally
            {
                if (key is not null)
                    CryptographicOperations.ZeroMemory(key);
            }

            var kept = records.OrderByDescending(stored => stored.Sequence).Take(maxEntries).ToList();
            await _files.WriteHistoryAsync(
                FailureHistoryCodec.Serialize(new FailureHistoryDocument(sequence + 1, kept)), cancellationToken).ConfigureAwait(false);
            return new FailureHistoryEntry(record.Id, record.OccurredAtUtc, Unencrypted: key is null, KeyReplaced: keyReplaced);
        }
        catch (Exception exception) when (FailureHistoryFiles.IsStorageException(exception))
        {
            // Keep the earlier key error: it explains why the save fell back to an unencrypted record.
            var error = StorageUnavailable(keyError is null ? exception : new AggregateException(exception, keyError));
            // The replacement already happened; this save is the only one that can report it.
            throw keyReplaced ? new FailureHistoryException(error.Code, $"{error.Message} {KeyReplacedMessage}", error.InnerException) : error;
        }
    }

    public async Task<IReadOnlyList<FailureRecord>> ReadRecordsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var document = FailureHistoryCodec.Parse(await _files.ReadHistoryAsync(cancellationToken).ConfigureAwait(false));
            if (document.Records.All(stored => stored.Kind == StoredFailureRecord.Plain))
                return Open(document, key: null);
            var (key, keyError) = await GetKeyAsync(createIfMissing: false, replaced: null, cancellationToken).ConfigureAwait(false);
            if (key is null)
                throw KeyUnavailable(keyError);
            try { return Open(document, key); }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
        catch (Exception exception) when (FailureHistoryFiles.IsStorageException(exception)) { throw StorageUnavailable(exception); }
    }

    public async Task<FailureRecord?> ReadPlainAsync(string? id, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = FailureHistoryCodec.Parse(await _files.ReadHistoryAsync(cancellationToken).ConfigureAwait(false));
            var selected = id is null
                ? document.Records.MaxBy(stored => stored.Sequence)
                : document.Records.FirstOrDefault(stored => stored.Id == id);
            return selected?.Kind == StoredFailureRecord.Plain ? FailureHistoryCodec.Open(selected, key: null) : null;
        }
        catch (Exception exception) when (FailureHistoryFiles.IsStorageException(exception)) { throw StorageUnavailable(exception); }
    }

    public async Task<IReadOnlyList<FailureHistoryEntry>> ReadIndexAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var document = FailureHistoryCodec.Parse(await _files.ReadHistoryAsync(cancellationToken).ConfigureAwait(false));
            return document.Records
                .OrderByDescending(stored => stored.Sequence)
                .Select(stored => new FailureHistoryEntry(stored.Id, stored.OccurredAt, stored.Kind == StoredFailureRecord.Plain))
                .ToList();
        }
        catch (Exception exception) when (FailureHistoryFiles.IsStorageException(exception)) { throw StorageUnavailable(exception); }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var writerLock = await _files.AcquireWriterLockAsync(cancellationToken).ConfigureAwait(false);
            _files.DeleteControlledData(cancellationToken);
        }
        catch (Exception exception) when (FailureHistoryFiles.IsStorageException(exception))
        {
            throw new FailureHistoryException("failure_history_clear_incomplete",
                "Failure history could not be cleared. Run 'az config set --key agent.recovery.trigger --value off' again.", exception);
        }
    }

    private static string NewId(uint terminalSessionId, FailureHistoryDocument document)
    {
        string id;
        do { id = FailureId.Create(terminalSessionId); }
        while (document.Records.Any(stored => stored.Id == id));
        return id;
    }

    private static List<FailureRecord> Open(FailureHistoryDocument document, byte[]? key) =>
        document.Records
            .OrderByDescending(stored => stored.Sequence)
            .Select(stored => FailureHistoryCodec.Open(stored, key))
            .OfType<FailureRecord>()
            .ToList();

    /// <summary>
    /// Returns the key, or null with the key-store exception when key storage is unavailable. When the key is confirmed absent and
    /// <paramref name="createIfMissing"/> is false, returns an empty array: encrypted records are treated as absent.
    /// </summary>
    private async Task<(byte[]? Key, Exception? Error)> GetKeyAsync(bool createIfMissing, Action? replaced, CancellationToken cancellationToken)
    {
        byte[]? key = null;
        try
        {
            key = await _keyStore.GetKeyAsync(createIfMissing, replaced, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (key is not { Length: 32 })
                throw new FailureHistoryException("failure_history_key_invalid", "The protected failure-history key is invalid.");
            var owned = key;
            key = null;
            return (owned, null);
        }
        catch (FailureHistoryException exception) when (exception.Code == KeyMissingCode && !createIfMissing)
        {
            return ([], null);
        }
        catch (FailureHistoryException exception) when (exception.Code == KeyUnavailableCode)
        {
            return KeyStoreUnavailable(exception, createIfMissing);
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or SecurityException or CryptographicException or FormatException or
            PlatformNotSupportedException or DllNotFoundException or EntryPointNotFoundException)
        {
            return KeyStoreUnavailable(exception, createIfMissing);
        }
        finally
        {
            if (key is not null)
                CryptographicOperations.ZeroMemory(key);
        }
    }

    private (byte[]? Key, Exception? Error) KeyStoreUnavailable(Exception exception, bool createIfMissing)
    {
        // Saving falls back to a plain record, so the cause is only visible at debug level.
        if (createIfMissing)
            _logger?.Debug($"[{nameof(EncryptedFailureHistoryStore)}] Failure-history key unavailable; saving unencrypted: {exception}");
        return (null, exception);
    }

    public const string KeyReplacedMessage =
        "The failure-history key was damaged and has been replaced. Earlier failures can no longer be read.";

    private static FailureHistoryException KeyUnavailable(Exception? innerException) =>
        new(KeyUnavailableCode, "The protected failure-history key is unavailable.", innerException);

    private static FailureHistoryException StorageUnavailable(Exception innerException) =>
        new("failure_history_io", "Failure history could not be accessed or saved. Check its directory permissions and available disk space.", innerException);
}
