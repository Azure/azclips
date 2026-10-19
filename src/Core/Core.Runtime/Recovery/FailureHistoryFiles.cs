// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using Core.Abstractions;
using Core.Abstractions.Recovery;

namespace Core.Runtime.Recovery;

internal sealed class FailureHistoryFiles
{
    internal const string HistoryFileName = "history.v2.bin";
    internal const string LegacyHistoryFileName = "history.v1.bin";
    internal const string PlaintextHistoryFileName = "history.latest.plain.json";
    internal const string ControlFileName = "control.v1.json";
    internal const string LockFileName = "history.lock";

    private readonly TimeSpan _lockTimeout;
    private readonly Action<string, string> _replace;
    private readonly Action<string> _delete;

    internal FailureHistoryFiles(
        string? directory = null,
        TimeSpan? lockTimeout = null,
        Action<string, string>? replace = null,
        Action<string>? delete = null)
    {
        DirectoryPath = Path.GetFullPath(directory ?? HomePaths.GetDefaultFailureHistoryDirectory());
        _lockTimeout = lockTimeout ?? TimeSpan.FromSeconds(2);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_lockTimeout, TimeSpan.Zero);
        _replace = replace ?? (static (source, target) => File.Move(source, target, overwrite: true));
        _delete = delete ?? File.Delete;
    }

    internal string DirectoryPath { get; }
    internal string HistoryPath => Path.Combine(DirectoryPath, HistoryFileName);
    internal string ControlPath => Path.Combine(DirectoryPath, ControlFileName);
    internal string PlaintextHistoryPath => Path.Combine(DirectoryPath, PlaintextHistoryFileName);

    internal async Task<FileStream> AcquireWriterLockAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (OperatingSystem.IsWindows())
            Directory.CreateDirectory(DirectoryPath);
        else
            Directory.CreateDirectory(DirectoryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Never unlink the lock: Unix writers must continue to lock the same inode.
                return new FileStream(Path.Combine(DirectoryPath, LockFileName), CreateFileOptions(FileMode.OpenOrCreate));
            }
            catch (IOException exception) when ((exception.HResult & 0xffff) is 11 or 32 or 33 or 35)
            {
                var remaining = _lockTimeout - Stopwatch.GetElapsedTime(started);
                if (remaining <= TimeSpan.Zero)
                    throw new FailureHistoryException("failure_history_locked", "Failure history is busy. Try again shortly.");
                await Task.Delay(remaining < TimeSpan.FromMilliseconds(25) ? remaining : TimeSpan.FromMilliseconds(25), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    internal async Task WriteAtomicallyAsync(
        string target,
        string stagingPrefix,
        Func<FileStream, CancellationToken, Task> write,
        CancellationToken cancellationToken,
        bool ownerOnly = false)
    {
        var stagingPath = Path.Combine(DirectoryPath, $"{stagingPrefix}{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = ownerOnly && OperatingSystem.IsWindows()
                ? CreateOwnerOnlyWindowsFile(stagingPath)
                : new FileStream(stagingPath, CreateFileOptions(FileMode.CreateNew)))
            {
                await write(stream, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            _replace(stagingPath, target);
        }
        finally
        {
            try { File.Delete(stagingPath); }
            catch (Exception exception) when (IsStorageException(exception))
            {
                // Best effort; leftover staging files are removed by DeleteControlledData.
            }
        }
    }

    internal bool HasLegacyHistory() => Exists(Path.Combine(DirectoryPath, LegacyHistoryFileName));

    internal bool HasControlledData() => EnumerateEntries().Any(path => IsControlledDataFile(Path.GetFileName(path)));

    internal bool HasV2StateArtifacts() => EnumerateEntries().Any(path =>
    {
        var name = Path.GetFileName(path);
        return name is HistoryFileName or PlaintextHistoryFileName || FailureHistoryKeyNames.IsGenerationFile(name) ||
               IsStagingFile(name, ".history-v2-") || IsStagingFile(name, ".history-plain-") || IsStagingFile(name, ".control-");
    });

    internal void DeleteControlledData(CancellationToken cancellationToken)
    {
        foreach (var path in EnumerateEntries())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsControlledDataFile(Path.GetFileName(path)))
                _delete(path);
        }
    }

    private IEnumerable<string> EnumerateEntries()
    {
        try { return Directory.GetFileSystemEntries(DirectoryPath); }
        catch (DirectoryNotFoundException) { return []; }
    }

    private static bool IsControlledDataFile(string name) =>
        name is HistoryFileName or PlaintextHistoryFileName or LegacyHistoryFileName or FailureHistoryKeyNames.LegacyFileName ||
        FailureHistoryKeyNames.IsGenerationFile(name) || IsStagingFile(name, ".history-") ||
        IsStagingFile(name, ".history-v2-") || IsStagingFile(name, ".history-plain-") || IsStagingFile(name, ".control-");

    private static bool IsStagingFile(string name, string prefix) =>
        name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".tmp", StringComparison.Ordinal) &&
        Guid.TryParseExact(name.AsSpan(prefix.Length, name.Length - prefix.Length - 4), "N", out _);

    private static bool Exists(string path)
    {
        try { _ = File.GetAttributes(path); return true; }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException) { return false; }
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
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return options;
    }

    // Explicit, non-inherited DACL granting only the current user; a same-volume rename keeps it.
    [SupportedOSPlatform("windows")]
    private static FileStream CreateOwnerOnlyWindowsFile(string path)
    {
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        using var identity = WindowsIdentity.GetCurrent();
        security.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl, AccessControlType.Allow));
        return new FileInfo(path).Create(FileMode.CreateNew, FileSystemRights.Read | FileSystemRights.Write,
            FileShare.None, bufferSize: 4096, FileOptions.Asynchronous, security);
    }

    internal static bool IsStorageException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException;
}
