// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Security;
using Core.Abstractions;
using Core.Abstractions.Recovery;

namespace Core.Runtime.Recovery;

internal sealed class FailureHistoryFiles
{
    internal const string HistoryFileName = "history.v2.bin";
    internal const string LegacyHistoryFileName = "history.v1.bin";
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
        CancellationToken cancellationToken)
    {
        var stagingPath = Path.Combine(DirectoryPath, $"{stagingPrefix}{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(stagingPath, CreateFileOptions(FileMode.CreateNew)))
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
                // Staging files contain only ciphertext or non-sensitive allocator state.
            }
        }
    }

    internal bool HasLegacyHistory() => Exists(Path.Combine(DirectoryPath, LegacyHistoryFileName));

    internal bool HasControlledData() => EnumerateEntries().Any(path => IsControlledDataFile(Path.GetFileName(path)));

    internal bool HasV2StateArtifacts() => EnumerateEntries().Any(path =>
    {
        var name = Path.GetFileName(path);
        return name == HistoryFileName || FailureHistoryKeyNames.IsGenerationFile(name) ||
               IsStagingFile(name, ".history-v2-") || IsStagingFile(name, ".control-");
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
        name is HistoryFileName or LegacyHistoryFileName or FailureHistoryKeyNames.LegacyFileName ||
        FailureHistoryKeyNames.IsGenerationFile(name) || IsStagingFile(name, ".history-") ||
        IsStagingFile(name, ".history-v2-") || IsStagingFile(name, ".control-");

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

    internal static bool IsStorageException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException;
}
