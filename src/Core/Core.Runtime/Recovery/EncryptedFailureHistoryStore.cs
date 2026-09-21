// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Core.Abstractions;
using Core.Abstractions.Recovery;

namespace Core.Runtime.Recovery;

/// <summary>
/// Stores one authenticated, encrypted snapshot. Readers never create files or access the writer
/// lock; replacing the snapshot atomically keeps the records and their selection index together.
/// </summary>
public sealed class EncryptedFailureHistoryStore : IFailureHistoryStore
{
    public const string HistoryFileName = "history.v1.bin";
    public const string LockFileName = "history.lock";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers =
            {
                static typeInfo =>
                {
                    if (typeInfo.Type == typeof(FailureRecord))
                    {
                        typeInfo.Properties.Single(property => property.Name == "schemaVersion").IsRequired = true;
                    }
                },
            },
        },
    };

    private readonly IFailureHistoryKeyStore _keyStore;
    private readonly string _directory;
    private readonly string _historyPath;
    private readonly string _lockPath;
    private readonly TimeSpan _lockTimeout;
    private readonly Action<string, string> _replaceSnapshot;

    public EncryptedFailureHistoryStore(IFailureHistoryKeyStore keyStore, string? historyDirectory = null)
        : this(keyStore, historyDirectory, TimeSpan.FromSeconds(2), static (source, target) => File.Move(source, target, overwrite: true))
    {
    }

    internal EncryptedFailureHistoryStore(
        IFailureHistoryKeyStore keyStore,
        string? historyDirectory,
        TimeSpan lockTimeout,
        Action<string, string> replaceSnapshot)
    {
        ArgumentNullException.ThrowIfNull(keyStore);
        ArgumentNullException.ThrowIfNull(replaceSnapshot);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lockTimeout, TimeSpan.Zero);

        _keyStore = keyStore;
        _directory = Path.GetFullPath(historyDirectory ?? HomePaths.GetDefaultFailureHistoryDirectory());
        _historyPath = Path.Combine(_directory, HistoryFileName);
        _lockPath = Path.Combine(_directory, LockFileName);
        _lockTimeout = lockTimeout;
        _replaceSnapshot = replaceSnapshot;
    }

    public async Task<FailureHistoryEntry> SaveAsync(
        FailureRecord record,
        int maxEntries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEntries);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRecord(record);

        try
        {
            // Freeze the mutable parts before waiting for another writer.
            try
            {
                record = record with { Arguments = [.. record.Arguments], ErrorDetails = record.ErrorDetails?.Clone() };
            }
            catch (ObjectDisposedException)
            {
                throw SerializationFailure();
            }

            ValidateRecord(record);
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(_directory);
            }
            else
            {
                Directory.CreateDirectory(_directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            using var writerLock = await AcquireWriterLockAsync(cancellationToken).ConfigureAwait(false);
            var snapshot = await ReadSnapshotAsync(includeCiphertext: true, cancellationToken).ConfigureAwait(false);
            byte[]? key = null;
            try
            {
                key = await GetKeyAsync(createIfMissing: snapshot is null, cancellationToken).ConfigureAwait(false);
                IReadOnlyList<FailureRecord> previous = snapshot is null ? [] : DecryptRecords(snapshot, key);
                if (previous.Any(existing => existing.Id == record.Id))
                {
                    throw new FailureHistoryException("failure_history_duplicate_id", "A failure-history record with this ID already exists.");
                }

                var records = new List<FailureRecord>(Math.Min(maxEntries, previous.Count + 1)) { record };
                records.AddRange(previous.Take(maxEntries - 1));
                await WriteSnapshotAsync(records, key, cancellationToken).ConfigureAwait(false);
                return new FailureHistoryEntry(record.Id, record.OccurredAtUtc);
            }
            finally
            {
                if (key is not null)
                {
                    CryptographicOperations.ZeroMemory(key);
                }
            }
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
            throw StorageUnavailable();
        }
    }

    public async Task<IReadOnlyList<FailureRecord>> ReadRecordsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var snapshot = await ReadSnapshotAsync(includeCiphertext: true, cancellationToken).ConfigureAwait(false);
            if (snapshot is null)
            {
                return [];
            }

            var key = await GetKeyAsync(createIfMissing: false, cancellationToken).ConfigureAwait(false);
            try
            {
                return DecryptRecords(snapshot, key);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
            throw StorageUnavailable();
        }
    }

    public async Task<IReadOnlyList<FailureHistoryEntry>> ReadIndexAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var snapshot = await ReadSnapshotAsync(includeCiphertext: false, cancellationToken).ConfigureAwait(false);
            // Structural validation is possible without a key. The index's authenticity is checked
            // as AES-GCM associated data before any selected record can be used.
            return snapshot?.Entries ?? [];
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
            throw StorageUnavailable();
        }
    }

    private async Task<byte[]> GetKeyAsync(bool createIfMissing, CancellationToken cancellationToken)
    {
        byte[]? key = null;
        try
        {
            key = await _keyStore.GetKeyAsync(createIfMissing, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (key is not { Length: 32 })
            {
                throw new FailureHistoryException("failure_history_key_invalid", "The protected failure-history key is invalid.");
            }

            var ownedKey = key;
            key = null;
            return ownedKey;
        }
        catch (FailureHistoryException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or SecurityException or CryptographicException or
            FormatException or PlatformNotSupportedException or DllNotFoundException or EntryPointNotFoundException)
        {
            // Native key-service exceptions can contain sensitive provider diagnostics.
            throw new FailureHistoryException("failure_history_key_unavailable", "The protected failure-history key is unavailable.");
        }
        finally
        {
            if (key is not null)
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }

    private async Task<FailureHistorySnapshot?> ReadSnapshotAsync(bool includeCiphertext, CancellationToken cancellationToken)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(_historyPath, new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read | FileShare.Delete,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }

        await using (stream.ConfigureAwait(false))
        {
            return await FailureHistorySnapshot.ReadAsync(stream, includeCiphertext, cancellationToken).ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<FailureRecord> DecryptRecords(FailureHistorySnapshot snapshot, byte[] key)
    {
        var plaintext = new byte[snapshot.Ciphertext!.Length];
        try
        {
            try
            {
                using var aes = new AesGcm(key, FailureHistorySnapshot.TagSize);
                aes.Decrypt(snapshot.Nonce!, snapshot.Ciphertext, snapshot.Tag!, plaintext, snapshot.AuthenticatedHeader);
            }
            catch (CryptographicException)
            {
                throw new FailureHistoryException(
                    "failure_history_authentication_failed",
                    "Failure history could not be authenticated. Its protected key or encrypted data may be missing or damaged.");
            }

            FailureRecord[] records;
            try
            {
                records = JsonSerializer.Deserialize<FailureRecord[]>(plaintext, JsonOptions) ?? throw FailureHistorySnapshot.Corrupt();
            }
            catch (JsonException)
            {
                throw FailureHistorySnapshot.Corrupt();
            }

            if (records.Length != snapshot.Entries.Count)
            {
                throw FailureHistorySnapshot.Corrupt();
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < records.Length; index++)
            {
                var record = records[index];
                ValidateRecord(record);
                if (!ids.Add(record.Id) ||
                    record.Id != snapshot.Entries[index].Id ||
                    record.OccurredAtUtc != snapshot.Entries[index].OccurredAtUtc)
                {
                    throw FailureHistorySnapshot.Corrupt();
                }
            }

            return records;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private async Task WriteSnapshotAsync(
        IReadOnlyList<FailureRecord> records,
        byte[] key,
        CancellationToken cancellationToken)
    {
        byte[] plaintext;
        try
        {
            plaintext = JsonSerializer.SerializeToUtf8Bytes(records, JsonOptions);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw SerializationFailure();
        }

        byte[] ciphertext;
        byte[] header;
        byte[] nonce;
        var tag = new byte[FailureHistorySnapshot.TagSize];
        try
        {
            nonce = RandomNumberGenerator.GetBytes(FailureHistorySnapshot.NonceSize);
            header = FailureHistorySnapshot.CreateHeader(records, plaintext.Length);
            ciphertext = new byte[plaintext.Length];
            using var aes = new AesGcm(key, FailureHistorySnapshot.TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, header);
        }
        catch (CryptographicException)
        {
            throw SerializationFailure();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }

        var stagingPath = Path.Combine(_directory, $".history-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(stagingPath, CreateFileOptions(FileMode.CreateNew)))
            {
                await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(nonce, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(tag, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(ciphertext, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            _replaceSnapshot(stagingPath, _historyPath);
        }
        finally
        {
            try
            {
                File.Delete(stagingPath);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                // A leftover staging file contains only ciphertext, never plaintext or a key.
            }
        }
    }

    private async Task<FileStream> AcquireWriterLockAsync(CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Never delete this file: unlinking a held lock permits two locks on different
                // inodes on Unix. FileShare.None coordinates independent processes and instances.
                return new FileStream(_lockPath, CreateFileOptions(FileMode.OpenOrCreate));
            }
            catch (IOException exception) when (IsLockContention(exception))
            {
                var remaining = _lockTimeout - Stopwatch.GetElapsedTime(started);
                if (remaining <= TimeSpan.Zero)
                {
                    throw new FailureHistoryException("failure_history_locked", "Failure history is busy. Try again shortly.");
                }

                await Task.Delay(remaining < TimeSpan.FromMilliseconds(25) ? remaining : TimeSpan.FromMilliseconds(25), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static FileStreamOptions CreateFileOptions(FileMode mode)
    {
        var options = new FileStreamOptions
        {
            Mode = mode,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        return options;
    }

    private static bool IsLockContention(IOException exception)
        => (exception.HResult & 0xffff) is 11 or 32 or 33 or 35;

    private static bool IsStorageException(Exception exception)
        => exception is IOException or UnauthorizedAccessException or SecurityException;

    private static FailureHistoryException StorageUnavailable()
        => new("failure_history_io", "Failure history could not be accessed or saved. Check its directory permissions and available disk space.");

    private static FailureHistoryException SerializationFailure()
        => new("failure_history_invalid_record", "The failure record could not be securely serialized.");

    private static void ValidateRecord(FailureRecord? record)
    {
        if (record is null ||
            !FailureHistorySnapshot.IsCanonicalId(record.Id) ||
            record.OccurredAtUtc.Offset != TimeSpan.Zero ||
            record.OccurredAtUtc == default ||
            record.Arguments is null ||
            record.Arguments.Any(argument => argument is null) ||
            record.FailedCommand is null ||
            record.ErrorText is null ||
            record.ExitCode == 0)
        {
            throw new FailureHistoryException("failure_history_invalid_record", "The failure record has invalid or missing required fields.");
        }

        if (record.SchemaVersion != FailureRecord.CurrentSchemaVersion)
        {
            throw FailureHistorySnapshot.UnsupportedSchema();
        }
    }
}
