// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Core.Abstractions;

/// <summary>
/// Cross-process lock over a lock file (opened with <see cref="FileShare.None"/>), and locked
/// read-modify-write of shared JSON files such as <see cref="HomePaths.GetDefaultAgentStatePath"/>.
/// Writers serialize on the lock; readers do not take it.
/// </summary>
public static class FileLock
{
    public static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Waits up to <paramref name="wait"/> for the lock. Returns <c>null</c> when it is still
    /// held by another process; a zero wait tries once. Dispose the stream to release the lock.
    /// </summary>
    public static async Task<FileStream?> TryAcquireAsync(string lockPath, TimeSpan wait, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(lockPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (stopwatch.Elapsed < wait)
            {
                await Task.Delay(RetryDelay, ct).ConfigureAwait(false);
            }
            catch (IOException)
            {
                return null;
            }
        }
    }

    public static FileStream? TryAcquire(string lockPath, TimeSpan wait)
        => TryAcquireAsync(lockPath, wait).GetAwaiter().GetResult();

    /// <summary>Reads a JSON object without locking; a missing or unreadable file yields an empty object.</summary>
    public static JsonObject ReadJson(string path)
    {
        try
        {
            if (!File.Exists(path))
                return [];

            // Share delete so a reader never blocks a writer replacing the file.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return JsonNode.Parse(stream) is JsonObject document ? document : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Under <c>&lt;path&gt;.lock</c>, reads the current document, applies <paramref name="update"/>
    /// and replaces the file. Keys the update does not touch are kept. Returns <c>false</c> when
    /// the lock is not obtained within <paramref name="wait"/> (default <see cref="DefaultWait"/>).
    /// </summary>
    public static bool UpdateJson(string path, Action<JsonObject> update, TimeSpan? wait = null)
    {
        using var gate = TryAcquire($"{path}.lock", wait ?? DefaultWait);
        if (gate is null)
            return false;

        var document = ReadJson(path);
        update(document);
        var temp = $"{path}.{Environment.ProcessId}.tmp";
        File.WriteAllText(temp, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        // File.Replace succeeds while readers hold the target open with FileShare.Delete;
        // File.Move(overwrite) is refused on Windows in that case.
        if (File.Exists(path))
            File.Replace(temp, path, destinationBackupFileName: null);
        else
            File.Move(temp, path);
        return true;
    }
}
