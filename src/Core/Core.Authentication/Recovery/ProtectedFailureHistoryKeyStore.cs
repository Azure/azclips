// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
            // An inaccessible key file is unavailable, not missing: MSAL may report it as empty.
            CheckKeyFileAccess(Path.Combine(_directory, FailureHistoryKeyNames.FileName));
            var storage = _createStorage();
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
            return new MacOsFailureHistoryKeyStorage(Purpose, directoryId);

        EnsureNonInteractiveBackend(OperatingSystem.IsWindows());
        var properties = new StorageCreationPropertiesBuilder(FailureHistoryKeyNames.FileName, _directory).Build();

        var diagnostics = new StringWriter(CultureInfo.InvariantCulture);
        var trace = new TraceSource(Purpose, SourceLevels.Warning);
        trace.Listeners.Clear();
        trace.Listeners.Add(new TextWriterTraceListener(diagnostics));
        if (_logger is not null)
            trace.Listeners.Add(new DebugLogTraceListener(_logger));
        return new MsalProtectedHistoryKeyStorage(Storage.Create(properties, trace), diagnostics);
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
}

internal interface IProtectedHistoryKeyStorage
{
    /// <summary>Returns the key, or an empty array when storage is accessible and the key is absent.</summary>
    byte[] ReadData();
    void WriteData(byte[] data);
    /// <summary>Errors the backend reported without throwing, if it keeps any.</summary>
    string? Diagnostics => null;
}

internal sealed class MsalProtectedHistoryKeyStorage(Storage storage, StringWriter diagnostics) : IProtectedHistoryKeyStorage
{
    public string Diagnostics => diagnostics.ToString();
    public byte[] ReadData() => storage.ReadData();
    public void WriteData(byte[] data) => storage.WriteData(data);
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
