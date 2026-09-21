// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Core.Authentication.Recovery;

/// <summary>
/// Keeps an independent 256-bit history key in the current user's OS-protected storage.
/// Callers must serialize creation across processes, including the existence check for history.
/// </summary>
public sealed class ProtectedFailureHistoryKeyStore : IFailureHistoryKeyStore
{
    public const string KeyFileName = "master-key.v1.protected";
    private const string Purpose = "Microsoft.Azclips.FailureHistory.v1";

    private readonly string _directory;
    private readonly string _keyPath;
    private readonly Lazy<Storage> _storage;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ProtectedFailureHistoryKeyStore(string? historyDirectory = null)
    {
        _directory = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(historyDirectory ?? HomePaths.GetDefaultFailureHistoryDirectory()));
        _keyPath = Path.Combine(_directory, KeyFileName);
        _storage = new Lazy<Storage>(CreateStorage);
    }

    public async Task<byte[]> GetKeyAsync(bool createIfMissing, CancellationToken cancellationToken = default)
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
            var keyFileExists = CheckKeyFileAccess();
            var storage = _storage.Value;
            key = storage.ReadData();
            cancellationToken.ThrowIfCancellationRequested();
            if (key.Length == 32)
            {
                var ownedKey = key;
                key = null;
                return ownedKey;
            }

            if (key.Length != 0)
            {
                throw new FailureHistoryException("failure_history_key_invalid", "The protected failure-history key is invalid.");
            }

            // The file is a protected key on Windows and an OS-store marker on macOS/Linux.
            // Never overwrite an existing but unreadable or empty key, even without a snapshot.
            if (!createIfMissing || keyFileExists)
            {
                throw new FailureHistoryException(
                    "failure_history_key_missing",
                    "The protected failure-history key is missing or inaccessible. Existing history cannot be recovered without its key.");
            }

            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(_directory);
            }
            else
            {
                Directory.CreateDirectory(_directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            key = RandomNumberGenerator.GetBytes(32);
            storage.WriteData(key);
            // MSAL's file adapter retries and then swallows some I/O failures. Verify the actual
            // key, not its generic persistence probe, before ever encrypting a history snapshot.
            persisted = storage.ReadData();
            cancellationToken.ThrowIfCancellationRequested();
            if (persisted.Length != 32 || !CryptographicOperations.FixedTimeEquals(key, persisted))
            {
                throw KeyUnavailable();
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
            // Do not surface native diagnostics, which may include secret data or account details.
            throw KeyUnavailable();
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

    private bool CheckKeyFileAccess()
    {
        try
        {
            using var stream = new FileStream(_keyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return true;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    private Storage CreateStorage()
    {
        var canonicalDirectory = OperatingSystem.IsWindows() ? _directory.ToUpperInvariant() : _directory;
        var directoryId = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalDirectory)));
        var properties = new StorageCreationPropertiesBuilder(KeyFileName, _directory)
            .WithLinuxKeyring(
                Purpose,
                MsalCacheHelper.LinuxKeyRingDefaultCollection,
                "Azclips encrypted failure history",
                new KeyValuePair<string, string>("application", Purpose),
                new KeyValuePair<string, string>("directory", directoryId))
            .WithMacKeyChain(Purpose, directoryId)
            .Build();

        var trace = new TraceSource(Purpose, SourceLevels.Off);
        trace.Listeners.Clear();
        trace.Switch.Level = SourceLevels.Off;
        return Storage.Create(properties, trace);
    }

    private static FailureHistoryException KeyUnavailable()
        => new(
            "failure_history_key_unavailable",
            "OS-protected failure-history key storage is unavailable. Check that the current user's secure key service is accessible.");
}
