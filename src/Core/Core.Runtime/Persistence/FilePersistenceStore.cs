// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Runtime.Persistence;

/// <summary>Filesystem persistence rooted at the Azclips home, with atomic file replacement.</summary>
public sealed class FilePersistenceStore : IPersistenceStore
{
    public FilePersistenceStore(string? rootDirectory = null)
    {
        RootDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(
            string.IsNullOrWhiteSpace(rootDirectory) ? HomePaths.GetDefaultHomeRoot() : rootDirectory));
    }

    public string RootDirectory { get; }

    public string GetFilePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        return ResolvePath(relativePath, allowRoot: false);
    }

    public Task<string> EnsureDirectoryAsync(string relativePath = "", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        var path = ResolvePath(relativePath, allowRoot: true);
        cancellationToken.ThrowIfCancellationRequested();
        CreateDirectory(path);
        return Task.FromResult(path);
    }

    public async Task<byte[]?> ReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var path = GetFilePath(relativePath);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // Windows readers must allow rename/delete so atomic replacement can proceed.
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var content = new MemoryStream();
            await stream.CopyToAsync(content, cancellationToken);
            return content.ToArray();
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    public async Task WriteAsync(string relativePath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        var path = GetFilePath(relativePath);
        cancellationToken.ThrowIfCancellationRequested();
        CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous,
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            await using (var stream = new FileStream(temporaryPath, options))
            {
                await stream.WriteAsync(content, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (Exception failure)
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception cleanupFailure) when (cleanupFailure is IOException or UnauthorizedAccessException)
            {
                throw new AggregateException("Persistence write and temporary-file cleanup both failed.", failure, cleanupFailure);
            }
            throw;
        }
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var path = GetFilePath(relativePath);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            File.Delete(path);
        }
        catch (DirectoryNotFoundException)
        {
            // Deleting absent state is idempotent, even before the root has been created.
        }
        return Task.CompletedTask;
    }

    private string ResolvePath(string relativePath, bool allowRoot)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Persistence paths must be relative to the storage root.", nameof(relativePath));
        }

        var path = Path.GetFullPath(Path.Combine(RootDirectory, relativePath));
        var relative = Path.GetRelativePath(RootDirectory, path);
        if (Path.IsPathRooted(relative) || relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            (!allowRoot && relative == "."))
        {
            throw new ArgumentException("Persistence paths must remain inside the storage root.", nameof(relativePath));
        }

        // Do not follow component-owned links out of the configured root.
        var current = RootDirectory;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar))
        {
            if (segment == ".")
            {
                continue;
            }
            current = Path.Combine(current, segment);
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new ArgumentException("Persistence paths must not contain symbolic links.", nameof(relativePath));
                }
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        return path;
    }

    private static void CreateDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
