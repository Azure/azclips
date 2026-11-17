using System.Globalization;
using System.Text.Json.Nodes;
using Core.Abstractions;

namespace Core.Agent.AgentMode;

/// <summary>
/// Expires Agent program data and removes the pre-agent-directory locations once.
/// Every directory it may delete is derived from <see cref="HomePaths"/>; state.json
/// only holds timestamps and flags, never paths. All failures are silent.
/// </summary>
internal sealed class AgentDataCleanup(IPersistenceStore persistence)
{
    private readonly string _telemetryQueue = persistence.GetFilePath(HomePaths.TraceDirectoryName);
    internal static readonly TimeSpan Retention = TimeSpan.FromDays(90);
    internal static readonly TimeSpan Interval = TimeSpan.FromDays(1);
    private static readonly TimeSpan StateWriteWait = TimeSpan.FromSeconds(1);

    internal const string LastCleanupKey = "lastCleanup";
    internal const string OldLocationsRemovedKey = "oldLocationsRemoved";
    private const string LockFileName = "cleanup.lock";
    private const string CopilotSessionDirectoryName = "session-state";

    private static readonly string[] OldLocationNames =
    [
        "arm-cache",
        "session-state",
        "session-store.db",
        "session-store.db-shm",
        "session-store.db-wal",
        "installed-plugins",
        "trace",
        "traces",
    ];

    private static readonly string[] OldCopilotSessionNames = ["session-state"];
    private static readonly string[] OldCopilotDatabaseNames =
        ["session-store.db", "session-store.db-shm", "session-store.db-wal"];

    /// <summary>Starts the cleanup on a background thread; never throws and never waits.</summary>
    public void StartInBackground(string configuredTracePath)
    {
        _ = Task.Run(() => Run(configuredTracePath, HomePaths.GetDefaultAgentRoot(), HomePaths.GetDefaultHomeRoot(), _telemetryQueue, DateTime.UtcNow));
    }

    internal static void Run(string configuredTracePath, string agentRoot, string oldHomeRoot, string telemetryQueue, DateTime nowUtc)
    {
        try
        {
            if (!Directory.Exists(agentRoot))
                return;

            using var gate = FileLock.TryAcquire(Path.Combine(agentRoot, LockFileName), TimeSpan.Zero);
            if (gate is null)
                return;

            RunCore(configuredTracePath, agentRoot, oldHomeRoot, telemetryQueue, nowUtc);
        }
        catch
        {
            // Silent by design: retried on the next day's run.
        }
    }

    private static void RunCore(string configuredTracePath, string agentRoot, string oldHomeRoot, string telemetryQueue, DateTime nowUtc)
    {
        var statePath = Path.Combine(agentRoot, HomePaths.AgentStateFileName);
        var state = FileLock.ReadJson(statePath);
        if ((state[LastCleanupKey] as JsonValue)?.TryGetValue<string>(out var last) == true
            && DateTime.TryParse(last, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var lastUtc)
            && nowUtc - lastUtc < Interval)
        {
            return;
        }

        var cutoff = nowUtc - Retention;
        var tracePath = ResolveTracePath(configuredTracePath);

        // Only Copilot sessions are expired; the SDK database and other SDK folders are left alone.
        ExpireChildren(Path.Combine(agentRoot, HomePaths.AgentCopilotDirectoryName, CopilotSessionDirectoryName), cutoff);
        ExpireChildren(Path.Combine(agentRoot, HomePaths.AgentEvaluationDirectoryName), cutoff);

        // A user-set agent.trace.path is the user's; only the default location is expired.
        var defaultTrace = Path.Combine(agentRoot, HomePaths.AgentTraceDirectoryName);
        if (SamePath(tracePath, defaultTrace))
            ExpireChildren(defaultTrace, cutoff);

        var oldLocationsRemoved = false;
        if (!((state[OldLocationsRemovedKey] as JsonValue)?.TryGetValue<bool>(out var removed) == true && removed))
        {
            var allRemoved = true;
            // An Agent from the previous version may still be running on the old session store.
            var oldSessionsInUse = OldCopilotSessionNames.Any(name => AnyChildInUse(Path.Combine(oldHomeRoot, name)));
            foreach (var name in OldLocationNames)
            {
                var target = Path.Combine(oldHomeRoot, name);
                if (oldSessionsInUse && (OldCopilotSessionNames.Contains(name) || OldCopilotDatabaseNames.Contains(name)))
                {
                    allRemoved = false;
                    continue;
                }
                // With AZCLIPS_HOME both roots coincide and "traces" is the telemetry queue.
                if (SamePath(telemetryQueue, target))
                    continue;
                allRemoved &= TryDelete(target);
            }

            oldLocationsRemoved = allRemoved;
        }

        FileLock.UpdateJson(statePath, document =>
        {
            document[LastCleanupKey] = nowUtc.ToString("O", CultureInfo.InvariantCulture);
            if (oldLocationsRemoved)
                document[OldLocationsRemovedKey] = true;
        }, StateWriteWait);
    }

    private static void ExpireChildren(string root, DateTime cutoffUtc)
    {
        if (!Directory.Exists(root))
            return;

        foreach (var entry in Directory.EnumerateFileSystemEntries(root))
        {
            ExpireEntry(entry, cutoffUtc);
        }
    }

    private static void ExpireEntry(string path, DateTime cutoffUtc)
    {
        if (LastWriteUtc(path) < cutoffUtc)
            TryDelete(path);
    }

    private static bool AnyChildInUse(string directory)
        => Directory.Exists(directory)
            && Directory.EnumerateDirectories(directory).Any(IsInUse);

    // The SDK marks an open session with inuse.<pid>.lock; it stays in use while that process runs.
    private static bool IsInUse(string directory)
    {
        foreach (var marker in Directory.EnumerateFiles(directory, "inuse.*.lock"))
        {
            var pidText = Path.GetFileNameWithoutExtension(marker)["inuse.".Length..];
            if (!int.TryParse(pidText, NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
                return true;
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(pid);
                if (!process.HasExited)
                    return true;
            }
            catch (ArgumentException)
            {
                // No such process: the marker is stale.
            }
        }

        return false;
    }

    private static DateTime LastWriteUtc(string path)
        => Directory.Exists(path) ? Directory.GetLastWriteTimeUtc(path) : File.GetLastWriteTimeUtc(path);

    // Returns true when the path no longer exists afterwards.
    private static bool TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
            else
                File.Delete(path);
            return !Directory.Exists(path) && !File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private static string? ResolveTracePath(string configuredTracePath)
        => string.IsNullOrWhiteSpace(configuredTracePath)
            ? null
            : Path.GetFullPath(AgentModeEntry.ExpandHome(configuredTracePath));

    private static bool SamePath(string? left, string right)
        => left is not null && string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
