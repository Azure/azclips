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

    internal static bool IsLayerFile(string path) =>
        Path.GetFileNameWithoutExtension(path) is { } name &&
        name.StartsWith(FilePrefix, StringComparison.Ordinal) &&
        name.AsSpan(FilePrefix.Length).Contains('-');

    internal static bool TryGetLayerToken(string path, out string token)
    {
        token = string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        if (!name.StartsWith(FilePrefix, StringComparison.Ordinal))
            return false;

        var separator = name.LastIndexOf('-');
        if (separator < FilePrefix.Length + 1 ||
            !Guid.TryParseExact(name.AsSpan(separator + 1), "N", out _))
            return false;

        token = name[FilePrefix.Length..separator];
        return token.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character));
    }

    internal void Reject(string claimedFile, string reason)
    {
        try
        {
            var rejected = Path.ChangeExtension(claimedFile, ".rejected");
            File.Move(claimedFile, rejected);
            File.WriteAllText(rejected + ".diagnostic.json",
                System.Text.Json.JsonSerializer.Serialize(new { reason, rejectedAt = DateTimeOffset.UtcNow }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Cannot preserve rejected telemetry file diagnostics: {ex.GetType().Name}.");
        }
    }

    /// <summary>
    /// Reclaims abandoned claims, then takes ownership of pending files by renaming them.
    /// Returns only the files this caller won.
    /// </summary>
    internal IReadOnlyList<string> ClaimPending()
        => ClaimPending(out _);

    internal IReadOnlyList<string> ClaimPending(
        out IReadOnlyDictionary<string, string> reservationByClaimedFile)
    {
        reservationByClaimedFile = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!Directory.Exists(_directory))
            return Array.Empty<string>();

        using var directoryLock = TryAcquireClaimLock();
        if (directoryLock is null)
            return Array.Empty<string>();

        ReclaimStaleClaims();

        var claimed = new List<string>();
        var reservations = new TraceUploadReservationStore(_directory);
        var heldNames = reservations.GetHeldFileNames();
        var reservationOwners = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var reservation in reservations.ReadAll().Where(item => item.Document.Ready))
        {
            var pending = reservation.PendingFiles
                .Select(name => Path.Combine(_directory, name))
                .ToArray();
            if (pending.Any(path => !File.Exists(path)))
                continue;

            var reservationClaims = new List<(string Pending, string Claimed)>();
            var failed = false;
            foreach (var path in pending)
            {
                var target = Path.ChangeExtension(path, ClaimedExtension);
                try
                {
                    File.Move(path, target);
                    reservationClaims.Add((path, target));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    failed = true;
                    break;
                }
            }

            if (failed)
            {
                foreach (var (pendingPath, claimedPath) in reservationClaims)
                {
                    try { File.Move(claimedPath, pendingPath); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                }
                continue;
            }

            foreach (var (_, claimedPath) in reservationClaims)
            {
                claimed.Add(claimedPath);
                reservationOwners[claimedPath] = reservation.Path;
            }
        }

        foreach (var pending in Directory.GetFiles(_directory, PendingFilePattern))
        {
            if (heldNames.Contains(Path.GetFileName(pending)))
                continue;

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

        reservationByClaimedFile = reservationOwners;
        return claimed;
    }

    private FileStream? TryAcquireClaimLock()
    {
        try
        {
            return TraceUploadReservationStore.AcquireDirectoryLock(_directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Telemetry files could not be claimed: {ex.GetType().Name}.");
            return null;
        }
    }

    internal bool DeleteReservation(string reservationPath) =>
        new TraceUploadReservationStore(_directory).Delete(reservationPath);

    internal FileStream? TryStartUpload()
    {
        if (!Directory.Exists(_directory))
            return null;
        try
        {
            return new FileStream(Path.Combine(_directory, ".uploader.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Telemetry upload deferred; uploader lock unavailable: {ex.GetType().Name}.");
            return null;
        }
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
