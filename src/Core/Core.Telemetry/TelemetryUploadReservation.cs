// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Core.Abstractions;

namespace Core.Telemetry;

/// <summary>
/// A command's upload reservation: the invocation that owns it and the trace directory it was
/// written to. <see cref="TelemetryUploadReservation.MarkReady"/> needs both.
/// </summary>
public sealed record CommandUploadReservation(string InvocationId, string TraceDirectory);

public static class TelemetryUploadReservation
{
    /// <summary>
    /// Holds this command's trace file back from the uploader until <see cref="MarkReady"/>.
    /// <paramref name="traceDirectory"/> must be the directory that file is written to, so the
    /// reservation is cleaned up with it. Returns null when there is nothing to reserve.
    /// </summary>
    public static CommandUploadReservation? Begin(TelemetryInvocationContext context, string traceDirectory)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(traceDirectory);
        if (!context.Enabled ||
            !Guid.TryParseExact(context.InvocationId, "N", out _) ||
            !IsHexId(context.CommandTraceId, 32))
            return null;

        new TraceUploadReservationStore(traceDirectory)
            .Begin(context.CommandTraceId!, context.InvocationId);
        return new CommandUploadReservation(context.InvocationId, traceDirectory);
    }

    public static void MarkReady(CommandUploadReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        new TraceUploadReservationStore(reservation.TraceDirectory)
            .MarkReady(reservation.InvocationId);
    }

    private static bool IsHexId(string? value, int length) =>
        value is { Length: var actualLength } && actualLength == length &&
        value.Any(character => character != '0') && value.All(char.IsAsciiHexDigit);
}

internal sealed class TraceUploadReservationStore
{
    private const string FilePrefix = "traces-hold-";
    private const string FileSuffix = ".json";
    private static readonly TimeSpan AbandonedOwnerAge = TimeSpan.FromMinutes(5);
    private readonly string _directory;

    internal TraceUploadReservationStore(string directory) =>
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));

    internal static FileStream AcquireDirectoryLock(string directory)
    {
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            try
            {
                // Keep the lock file: unlinking it lets another process lock a different inode.
                return new FileStream(Path.Combine(directory, ".upload.lock"),
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (Directory.Exists(directory) &&
                                       Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(2))
            {
                Thread.Sleep(20);
            }
        }
    }

    internal void Begin(string traceId, string invocationId)
    {
        ValidateId(traceId, 32, nameof(traceId));
        ValidateGuid(invocationId, nameof(invocationId));
        Directory.CreateDirectory(_directory);
        using var directoryLock = AcquireDirectoryLock(_directory);

        var document = new ReservationDocument(
            Environment.ProcessId,
            Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks,
            DateTimeOffset.UtcNow,
            Ready: false,
            IncludeAgentFile: false);
        Write(ReservationPath(traceId, invocationId), document, overwrite: false);
    }

    internal void MarkReady(string invocationId)
    {
        if (!Directory.Exists(_directory))
            return;
        using var directoryLock = AcquireDirectoryLock(_directory);
        var reservation = FindByInvocation(invocationId);
        if (reservation is null)
            return;
        Write(reservation.Value.Path, reservation.Value.Document with
        {
            Ready = true,
            IncludeAgentFile = File.Exists(LayerPath(invocationId)),
        }, overwrite: true);
    }

    internal IReadOnlyList<Reservation> ReadAll()
    {
        if (!Directory.Exists(_directory))
            return [];

        var reservations = new List<Reservation>();
        foreach (var path in Directory.GetFiles(_directory, FilePrefix + "*" + FileSuffix))
        {
            if (!TryRead(path, out var traceId, out var invocationId, out var document))
            {
                Console.Error.WriteLine("Telemetry upload reservation is invalid; related files remain held.");
                continue;
            }

            if (!document.Ready && IsOwnerAbandoned(document))
            {
                document = document with
                {
                    Ready = true,
                    IncludeAgentFile = File.Exists(LayerPath(invocationId))
                };
                try
                {
                    Write(path, document, overwrite: true);
                    Console.Error.WriteLine("Telemetry upload reservation recovered after its owner exited.");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Console.Error.WriteLine("Telemetry upload reservation recovery failed.");
                    continue;
                }
            }

            reservations.Add(new Reservation(traceId, invocationId, path, document));
        }
        return reservations;
    }

    internal HashSet<string> GetHeldFileNames()
    {
        var held = new HashSet<string>(StringComparer.Ordinal);
        if (!Directory.Exists(_directory))
            return held;

        foreach (var path in Directory.GetFiles(_directory, FilePrefix + "*" + FileSuffix))
        {
            var stem = Path.GetFileNameWithoutExtension(path);
            var ids = stem[FilePrefix.Length..].Split('-', StringSplitOptions.None);
            if (ids.Length != 2 || !IsHexId(ids[0], 32) ||
                !Guid.TryParseExact(ids[1], "N", out _))
                continue;

            held.Add(TracePath(ids[0]));
            held.Add(AgentPath(ids[1]));
        }
        return held;
    }

    internal bool Delete(string path)
    {
        try
        {
            if (!File.Exists(path))
                return true;
            using var directoryLock = AcquireDirectoryLock(_directory);
            File.Delete(path);
            return !File.Exists(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Telemetry upload reservation could not be removed.");
            return false;
        }
    }

    internal static string TracePath(string traceId) =>
        TraceFileStore.FileNameFor(traceId);

    internal static string AgentPath(string invocationId) =>
        $"traces-agent-{invocationId}.log";

    internal static string ClaimedPath(string pendingPath) =>
        Path.ChangeExtension(pendingPath, ".uploading");

    private string LayerPath(string invocationId) =>
        Path.Combine(_directory, AgentPath(invocationId));

    private string ReservationPath(string traceId, string invocationId) =>
        Path.Combine(_directory, $"{FilePrefix}{traceId}-{invocationId}{FileSuffix}");

    private (string Path, ReservationDocument Document)? FindByInvocation(string invocationId)
    {
        ValidateGuid(invocationId, nameof(invocationId));
        if (!Directory.Exists(_directory))
            return null;

        foreach (var path in Directory.GetFiles(_directory, $"{FilePrefix}*-{invocationId}{FileSuffix}"))
        {
            if (TryRead(path, out _, out _, out var document))
                return (path, document);
        }
        return null;
    }

    private static bool TryRead(
        string path,
        out string traceId,
        out string invocationId,
        out ReservationDocument document)
    {
        traceId = string.Empty;
        invocationId = string.Empty;
        document = default!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var ids = stem[FilePrefix.Length..].Split('-', StringSplitOptions.None);
        if (ids.Length != 2 || !IsHexId(ids[0], 32) ||
            !Guid.TryParseExact(ids[1], "N", out _))
            return false;

        try
        {
            document = JsonSerializer.Deserialize<ReservationDocument>(File.ReadAllText(path))!;
            if (document is null || document.OwnerProcessId <= 0 ||
                document.OwnerStartUtcTicks <= 0)
                return false;
            traceId = ids[0];
            invocationId = ids[1];
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static bool IsOwnerAbandoned(ReservationDocument document)
    {
        if (DateTimeOffset.UtcNow - document.CreatedUtc < AbandonedOwnerAge)
            return false;
        try
        {
            using var owner = Process.GetProcessById(document.OwnerProcessId);
            return owner.StartTime.ToUniversalTime().Ticks != document.OwnerStartUtcTicks;
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static void Write(string path, ReservationDocument document, bool overwrite)
    {
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(document));
            File.Move(temporaryPath, path, overwrite);
        }
        finally
        {
            try { File.Delete(temporaryPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private static bool IsHexId(string value, int length) =>
        value.Length == length && value.Any(character => character != '0') &&
        value.All(char.IsAsciiHexDigit);

    private static void ValidateId(string value, int length, string parameterName)
    {
        if (!IsHexId(value, length))
            throw new ArgumentException("A trace ID must be nonzero hexadecimal text.", parameterName);
    }

    private static void ValidateGuid(string value, string parameterName)
    {
        if (!Guid.TryParseExact(value, "N", out _))
            throw new ArgumentException("An invocation ID must be a GUID in N format.", parameterName);
    }

    internal sealed record Reservation(
        string TraceId,
        string InvocationId,
        string Path,
        ReservationDocument Document)
    {
        internal IReadOnlyList<string> PendingFiles =>
            Document.IncludeAgentFile
                ? [TracePath(TraceId), AgentPath(InvocationId)]
                : [TracePath(TraceId)];
    }

    internal sealed record ReservationDocument(
        int OwnerProcessId,
        long OwnerStartUtcTicks,
        DateTimeOffset CreatedUtc,
        bool Ready,
        bool IncludeAgentFile);
}
