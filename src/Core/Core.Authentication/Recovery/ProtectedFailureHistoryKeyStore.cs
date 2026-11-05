// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Core.Authentication.Recovery;

/// <summary>
/// Keeps an independent 256-bit history key in the current user's OS-protected storage.
/// Callers must serialize creation across processes.
/// </summary>
public sealed class ProtectedFailureHistoryKeyStore : IFailureHistoryKeyStore
{
    private const string Purpose = "Microsoft.Azclips.FailureHistory";
    private const string KeyInvalidCode = "failure_history_key_invalid";

    private readonly string _directory;
    private readonly Func<IProtectedHistoryKeyStorage> _createStorage;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILoggingService? _logger;

    /// <param name="logger">Receives MSAL storage trace lines at debug level as they happen,
    /// so they are visible even when the caller stops this process before the key call returns.</param>
    public ProtectedFailureHistoryKeyStore(string? historyDirectory = null, ILoggingService? logger = null)
        : this(historyDirectory, (Func<IProtectedHistoryKeyStorage>?)null)
    {
        _logger = logger;
    }

    internal ProtectedFailureHistoryKeyStore(string? historyDirectory, Func<IProtectedHistoryKeyStorage>? createStorage)
    {
        _directory = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(historyDirectory ?? HomePaths.GetDefaultFailureHistoryDirectory()));
        _createStorage = createStorage ?? CreateStorage;
    }

    public Task<byte[]> GetKeyAsync(bool createIfMissing, CancellationToken cancellationToken = default)
        => GetKeyAsync(createIfMissing, replaced: null, cancellationToken);

    /// <inheritdoc />
    public async Task<byte[]> GetKeyAsync(bool createIfMissing, Action? replaced, CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false))
        {
            throw KeyUnavailable();
        }

        byte[]? key = null;
        byte[]? persisted = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // An inaccessible key file is unavailable, not missing: MSAL may report it as empty.
            CheckKeyFileAccess(Path.Combine(_directory, FailureHistoryKeyNames.FileName));
            var storage = _createStorage();
            try
            {
                key = storage.ReadData();
                if (key.Length is not (0 or 32))
                    throw KeyInvalid();
            }
            // Only the save path replaces a damaged key: its caller holds the cross-process writer lock.
            catch (FailureHistoryException exception) when (exception.Code == KeyInvalidCode && createIfMissing)
            {
                if (key is not null)
                    CryptographicOperations.ZeroMemory(key);
                key = [];
                _logger?.Debug($"[{nameof(ProtectedFailureHistoryKeyStore)}] Replacing the damaged failure-history key: {exception}");
                try
                {
                    storage.DeleteData();
                }
                // A key that cannot be deleted stays invalid: never fall back to an unencrypted record for it.
                catch (Exception deleteError) when (deleteError is
                    FailureHistoryException or IOException or UnauthorizedAccessException or SecurityException)
                {
                    throw new FailureHistoryException(KeyInvalidCode,
                        "The protected failure-history key is invalid and could not be replaced.", deleteError);
                }

                // The old key is gone now, so this save is the only one that can report it, even if creating the new key fails.
                replaced?.Invoke();
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (key.Length == 32)
            {
                var ownedKey = key;
                key = null;
                return ownedKey;
            }

            // Storage reported the key as absent, not locked or unavailable: create one when allowed.
            if (!createIfMissing)
            {
                throw new FailureHistoryException(
                    "failure_history_key_missing",
                    "The protected failure-history key is missing or inaccessible. Existing history cannot be recovered without its key.");
            }

            EnsureKeyDirectory();
            key = RandomNumberGenerator.GetBytes(32);
            storage.WriteData(key);
            // MSAL's file adapter retries and then swallows some I/O failures. Verify the actual
            // key, not its generic persistence probe, before ever encrypting a history snapshot.
            persisted = storage.ReadData();
            cancellationToken.ThrowIfCancellationRequested();
            if (persisted.Length != 32 || !CryptographicOperations.FixedTimeEquals(key, persisted))
            {
                // MSAL reports the swallowed write error only in its trace.
                throw KeyUnavailable(storage.Diagnostics is { Length: > 0 } trace ? new IOException(trace) : null);
            }

            var createdKey = key;
            key = null;
            return createdKey;
        }
        catch (FailureHistoryException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        // MSAL 4.83.1's InteropException is internal and derives directly from Exception.
        // It must propagate: recognizing it by reflection would couple us to a private API.
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or SecurityException or CryptographicException or
            FormatException or PlatformNotSupportedException or DllNotFoundException or EntryPointNotFoundException or
            MsalCachePersistenceException)
        {
            throw KeyUnavailable(exception);
        }
        finally
        {
            if (key is not null)
            {
                CryptographicOperations.ZeroMemory(key);
            }

            if (persisted is not null)
            {
                CryptographicOperations.ZeroMemory(persisted);
            }

            _gate.Release();
        }
    }

    private void EnsureKeyDirectory()
    {
        if (OperatingSystem.IsWindows())
            Directory.CreateDirectory(_directory);
        else
            Directory.CreateDirectory(_directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static void CheckKeyFileAccess(string keyPath)
    {
        try
        {
            using var stream = new FileStream(keyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
        }
    }

    private IProtectedHistoryKeyStorage CreateStorage()
    {
        var canonicalDirectory = OperatingSystem.IsWindows() ? _directory.ToUpperInvariant() : _directory;
        var directoryId = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalDirectory)));
        if (OperatingSystem.IsLinux())
            return new LinuxFailureHistoryKeyStorage(Purpose, directoryId);
        if (OperatingSystem.IsMacOS())
            return new MacOsFailureHistoryKeyStorage(Purpose, directoryId, _logger);

        EnsureNonInteractiveBackend(OperatingSystem.IsWindows());
        var properties = new StorageCreationPropertiesBuilder(FailureHistoryKeyNames.FileName, _directory).Build();

        var diagnostics = new StringWriter(CultureInfo.InvariantCulture);
        var trace = new TraceSource(Purpose, SourceLevels.Warning);
        trace.Listeners.Clear();
        trace.Listeners.Add(new TextWriterTraceListener(diagnostics));
        if (_logger is not null)
            trace.Listeners.Add(new DebugLogTraceListener(_logger));
        return new MsalProtectedHistoryKeyStorage(
            Storage.Create(properties, trace), diagnostics, Path.Combine(_directory, FailureHistoryKeyNames.FileName));
    }

    internal static void EnsureNonInteractiveBackend(bool isWindows)
    {
        // Only the Windows MSAL fallback guarantees CRYPTPROTECT_UI_FORBIDDEN.
        // Unix platforms must use the explicitly noninteractive adapters above.
        if (!isWindows)
            throw new FailureHistoryException("failure_history_key_unavailable",
                "No noninteractive OS-protected history backend is registered for this platform. No native key access was attempted.");
    }

    private static FailureHistoryException KeyUnavailable(Exception? innerException = null)
        => new(
            "failure_history_key_unavailable",
            "OS-protected failure-history key storage is unavailable. Check that the current user's secure key service is accessible.", innerException);

    internal static FailureHistoryException KeyInvalid()
        => new(KeyInvalidCode, "The protected failure-history key is invalid.");
}

internal interface IProtectedHistoryKeyStorage
{
    /// <summary>
    /// Returns the key, or an empty array when storage is accessible and the key is absent.
    /// Throws failure_history_key_invalid only for a stored item that can never become readable.
    /// </summary>
    byte[] ReadData();
    void WriteData(byte[] data);
    /// <summary>Deletes the stored key; succeeds when it is already absent. Never prompts.</summary>
    void DeleteData();
    /// <summary>Errors the backend reported without throwing, if it keeps any.</summary>
    string? Diagnostics => null;
}

internal sealed class MsalProtectedHistoryKeyStorage(Storage storage, StringWriter diagnostics, string keyPath) : IProtectedHistoryKeyStorage
{
    // CryptProtectData output starts with its version and provider GUID. DPAPI error codes do not
    // distinguish a damaged file from a temporarily undecryptable one; the blob's structure does.
    private static readonly byte[] DpapiBlobHeader =
    [
        0x01, 0x00, 0x00, 0x00, 0xd0, 0x8c, 0x9d, 0xdf, 0x01, 0x15,
        0xd1, 0x11, 0x8c, 0x7a, 0x00, 0xc0, 0x4f, 0xc2, 0x97, 0xeb,
    ];

    // Blob larger than any key file this class writes; a bigger file was not written by it.
    private const int MaxBlobLength = 64 * 1024;

    public string Diagnostics => diagnostics.ToString();

    public byte[] ReadData()
    {
        if (!IsWellFormedBlob(out var present))
            throw ProtectedFailureHistoryKeyStore.KeyInvalid();
        var data = storage.ReadData();
        // A key file that decrypts to nothing was not written by WriteData, which never stores an empty key.
        return data.Length == 0 && present ? throw ProtectedFailureHistoryKeyStore.KeyInvalid() : data;
    }

    public void WriteData(byte[] data) => storage.WriteData(data);

    public void DeleteData() => File.Delete(keyPath);

    private bool IsWellFormedBlob(out bool present)
    {
        byte[] blob;
        present = false;
        try
        {
            using var stream = new FileStream(keyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            // An empty file is an absent key, as MSAL reports it.
            if (stream.Length == 0)
                return true;
            present = true;
            if (stream.Length > MaxBlobLength)
                return false;
            blob = new byte[stream.Length];
            stream.ReadExactly(blob);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return true;
        }

        return HasDpapiLayout(blob);
    }

    // DPAPI_BLOB layout: header, master key version and GUID, flags, then length-prefixed description,
    // salt, HMAC key, second HMAC key, data and signature, with fixed algorithm fields between them.
    // A truncated or extended file cannot match this layout exactly.
    internal static bool HasDpapiLayout(ReadOnlySpan<byte> blob)
    {
        if (!blob.StartsWith(DpapiBlobHeader))
            return false;
        var offset = DpapiBlobHeader.Length + 4 + 16 + 4;
        return Skip(blob, ref offset, prefixed: true) && Skip(blob, ref offset, fixedLength: 8) &&
            Skip(blob, ref offset, prefixed: true) && Skip(blob, ref offset, prefixed: true) &&
            Skip(blob, ref offset, fixedLength: 8) && Skip(blob, ref offset, prefixed: true) &&
            Skip(blob, ref offset, prefixed: true) && Skip(blob, ref offset, prefixed: true) &&
            offset == blob.Length;
    }

    private static bool Skip(ReadOnlySpan<byte> blob, ref int offset, bool prefixed = false, int fixedLength = 0)
    {
        long length = fixedLength;
        if (prefixed)
        {
            if (offset > blob.Length - 4)
                return false;
            length = BinaryPrimitives.ReadUInt32LittleEndian(blob[offset..]);
            offset += 4;
        }

        if (length > blob.Length - offset)
            return false;
        offset += (int)length;
        return true;
    }
}

internal sealed class DebugLogTraceListener(ILoggingService logger) : TraceListener
{
    private readonly StringBuilder _line = new();

    public override void Write(string? message)
    {
        lock (_line)
            _line.Append(message);
    }

    public override void WriteLine(string? message)
    {
        string line;
        lock (_line)
        {
            line = _line.Append(message).ToString();
            _line.Clear();
        }
        logger.Debug($"[{nameof(ProtectedFailureHistoryKeyStore)}] {line}");
    }
}
