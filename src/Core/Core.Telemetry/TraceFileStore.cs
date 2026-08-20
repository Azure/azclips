// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry;

/// <summary>
/// Operates on one trace directory: file naming, and the rename-based claim protocol that
/// lets concurrent uploaders share it. Which directory is a deployment concern owned by
/// <see cref="Core.Abstractions.HomePaths"/>; this type only ever uses the one it is given.
/// </summary>
internal sealed class TraceFileStore
{
    private const string FilePrefix = "traces-";
    private const string PendingExtension = ".log";
    private const string ClaimedExtension = ".uploading";

    /// <summary>A claim older than this is treated as abandoned by a crashed uploader.</summary>
    private static readonly TimeSpan StaleClaimAge = TimeSpan.FromMinutes(5);

    private readonly string _directory;

    internal TraceFileStore(string directory)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
    }

    /// <summary>The file name a given trace buffers into.</summary>
    internal static string FileNameFor(string traceId) =>
        $"{FilePrefix}{traceId}{PendingExtension}";

    /// <summary>
    /// Search pattern matching every unclaimed trace file, for readers that want the files
    /// without the claim protocol.
    /// </summary>
    internal static string PendingFilePattern => FilePrefix + "*" + PendingExtension;

    private static string ClaimedFilePattern => FilePrefix + "*" + ClaimedExtension;

    /// <summary>
    /// Reclaims abandoned claims, then takes ownership of pending files by renaming them.
    /// Returns only the files this caller won.
    /// </summary>
    internal IReadOnlyList<string> ClaimPending()
    {
        if (!Directory.Exists(_directory))
            return Array.Empty<string>();

        ReclaimStaleClaims();

        var claimed = new List<string>();

        foreach (var pending in Directory.GetFiles(_directory, PendingFilePattern))
        {
            var target = Path.ChangeExtension(pending, ClaimedExtension);
            try
            {
                File.Move(pending, target);
                claimed.Add(target);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Another uploader won the file, or a writer still holds it.
            }
        }

        return claimed;
    }

    /// <summary>Reads a claimed file. Returns false when it cannot be read.</summary>
    internal bool TryRead(string claimedFile, out string content)
    {
        try
        {
            content = File.ReadAllText(claimedFile);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Vanished or locked.
            content = string.Empty;
            return false;
        }
    }

    /// <summary>Removes a claimed file.</summary>
    internal void Delete(string claimedFile)
    {
        try
        {
            File.Delete(claimedFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Already gone or locked.
        }
    }

    private void ReclaimStaleClaims()
    {
        var cutoff = DateTime.UtcNow - StaleClaimAge;

        foreach (var stale in Directory.GetFiles(_directory, ClaimedFilePattern))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(stale) < cutoff)
                    File.Move(stale, Path.ChangeExtension(stale, PendingExtension));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Another uploader reclaimed it first.
            }
        }
    }
}
