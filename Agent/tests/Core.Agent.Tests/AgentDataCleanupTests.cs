using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Agent.AgentMode;
using Xunit;

namespace Core.Agent.Tests;

public sealed class AgentDataCleanupTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _root = Directory.CreateTempSubdirectory("azclips-cleanup-").FullName;
    private string OldHome => Path.Combine(_root, "home");
    private string AgentRoot => Path.Combine(OldHome, "agent");
    private string TelemetryQueue => Path.Combine(_root, "local", "traces");
    private string DefaultTrace => Path.Combine(AgentRoot, HomePaths.AgentTraceDirectoryName);
    private string Sessions => Path.Combine(AgentRoot, HomePaths.AgentCopilotDirectoryName, "session-state");
    private string Evaluation => Path.Combine(AgentRoot, HomePaths.AgentEvaluationDirectoryName);
    private string StatePath => Path.Combine(AgentRoot, HomePaths.AgentStateFileName);

    public AgentDataCleanupTests()
    {
        Directory.CreateDirectory(AgentRoot);
        Directory.CreateDirectory(OldHome);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Expires_entries_older_than_ninety_days_and_keeps_newer_ones()
    {
        var oldTrace = File(DefaultTrace, "old.jsonl", ageDays: 91);
        var newTrace = File(DefaultTrace, "new.jsonl", ageDays: 89);
        var oldSession = Dir(Sessions, "s-old", ageDays: 91);
        var newSession = Dir(Sessions, "s-new", ageDays: 89);
        var oldRun = Dir(Evaluation, "run-old", ageDays: 91);
        var newRun = Dir(Evaluation, "run-new", ageDays: 89);

        Run(DefaultTrace);

        Assert.False(System.IO.File.Exists(oldTrace));
        Assert.True(System.IO.File.Exists(newTrace));
        Assert.False(Directory.Exists(oldSession));
        Assert.True(Directory.Exists(newSession));
        Assert.False(Directory.Exists(oldRun));
        Assert.True(Directory.Exists(newRun));
    }

    [Fact]
    public void Expires_only_copilot_sessions()
    {
        var copilot = Path.Combine(AgentRoot, HomePaths.AgentCopilotDirectoryName);
        var database = File(copilot, "session-store.db", ageDays: 200);
        var other = File(copilot, "other.json", ageDays: 200);
        var plugin = Dir(Path.Combine(copilot, "installed-plugins"), "p-old", ageDays: 200);
        var oldSession = Dir(Sessions, "s-old", ageDays: 91);
        var newSession = Dir(Sessions, "s-new", ageDays: 89);

        Run(DefaultTrace);

        Assert.True(System.IO.File.Exists(database));
        Assert.True(System.IO.File.Exists(other));
        Assert.True(Directory.Exists(plugin));
        Assert.False(Directory.Exists(oldSession));
        Assert.True(Directory.Exists(newSession));
    }

    [Fact]
    public void Keeps_keys_other_writers_put_in_state_json()
    {
        System.IO.File.WriteAllText(StatePath, """{"version":1,"recovery":{"hintsShown":3}}""");

        Run(DefaultTrace);

        var state = ReadState();
        Assert.Equal(1, state["version"]!.GetValue<int>());
        Assert.Equal(3, state["recovery"]!["hintsShown"]!.GetValue<int>());
        Assert.NotNull(state[AgentDataCleanup.LastCleanupKey]);
    }

    [Fact]
    public void Never_touches_a_user_set_trace_path_or_the_default_one_then()
    {
        var userTrace = Path.Combine(_root, "mytraces");
        var userFile = File(userTrace, "old.jsonl", ageDays: 200);
        var defaultFile = File(DefaultTrace, "old.jsonl", ageDays: 200);

        Run(userTrace);

        Assert.True(System.IO.File.Exists(userFile));
        Assert.True(System.IO.File.Exists(defaultFile));
    }

    [Fact]
    public void Missing_default_trace_path_is_not_an_error()
    {
        Run(DefaultTrace);

        Assert.False(Directory.Exists(DefaultTrace));
        Assert.NotNull(ReadState()[AgentDataCleanup.LastCleanupKey]);
    }

    [Fact]
    public void Removes_old_locations_once_and_leaves_user_files()
    {
        Dir(OldHome, "arm-cache", ageDays: 0);
        Dir(OldHome, "session-state", ageDays: 0);
        File(OldHome, "session-store.db", ageDays: 0);
        Dir(OldHome, "trace", ageDays: 0);
        var config = File(OldHome, "config.json", ageDays: 0);
        var perfEval = Dir(OldHome, "perf-eval", ageDays: 0);
        var cache = Dir(OldHome, "cache", ageDays: 0);

        Run(DefaultTrace);

        Assert.False(Directory.Exists(Path.Combine(OldHome, "arm-cache")));
        Assert.False(Directory.Exists(Path.Combine(OldHome, "session-state")));
        Assert.False(System.IO.File.Exists(Path.Combine(OldHome, "session-store.db")));
        Assert.False(Directory.Exists(Path.Combine(OldHome, "trace")));
        Assert.True(System.IO.File.Exists(config));
        Assert.True(Directory.Exists(perfEval));
        Assert.True(Directory.Exists(cache));
        Assert.True(ReadState()[AgentDataCleanup.OldLocationsRemovedKey]!.GetValue<bool>());

        var recreated = Dir(OldHome, "arm-cache", ageDays: 0);
        Run(DefaultTrace, Now.AddDays(2));
        Assert.True(Directory.Exists(recreated));
    }

    [Fact]
    public void Runs_at_most_once_per_day()
    {
        Run(DefaultTrace);
        var late = File(DefaultTrace, "old.jsonl", ageDays: 200);

        Run(DefaultTrace, Now.AddHours(23));
        Assert.True(System.IO.File.Exists(late));

        Run(DefaultTrace, Now.AddHours(25));
        Assert.False(System.IO.File.Exists(late));
    }

    [Fact]
    public void Paths_in_state_json_cannot_widen_what_is_deleted()
    {
        var outside = Dir(_root, "precious", ageDays: 200);
        System.IO.File.WriteAllText(StatePath, $$"""{"roots":["{{outside.Replace("\\", "\\\\")}}"],"oldLocationsRemoved":"{{outside.Replace("\\", "\\\\")}}"}""");

        Run(DefaultTrace);

        Assert.True(Directory.Exists(outside));
    }

    [Fact]
    public void A_locked_file_is_skipped_silently_and_retried_the_next_day()
    {
        var locked = File(DefaultTrace, "locked.jsonl", ageDays: 200);
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Run(DefaultTrace);
        }

        if (!OperatingSystem.IsWindows())
            return;

        Assert.True(System.IO.File.Exists(locked));
        Run(DefaultTrace, Now.AddDays(1));
        Assert.False(System.IO.File.Exists(locked));
    }

    [Fact]
    public void Skips_when_another_process_holds_the_cleanup_lock()
    {
        var old = File(DefaultTrace, "old.jsonl", ageDays: 200);
        using (new FileStream(Path.Combine(AgentRoot, "cleanup.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            Run(DefaultTrace);
        }

        Assert.True(System.IO.File.Exists(old));
    }

    [Fact]
    public void Removes_the_old_default_traces_directory()
    {
        File(Path.Combine(OldHome, "traces"), "trace-1.jsonl", ageDays: 0);

        Run(DefaultTrace);

        Assert.False(Directory.Exists(Path.Combine(OldHome, "traces")));
    }

    [Fact]
    public void Keeps_the_telemetry_queue_when_home_and_local_roots_coincide()
    {
        var shared = Path.Combine(_root, "shared");
        var agentRoot = Path.Combine(shared, "agent");
        Directory.CreateDirectory(agentRoot);
        var queue = File(Path.Combine(shared, "traces"), "traces-1.log", ageDays: 0);

        AgentDataCleanup.Run(Path.Combine(agentRoot, "trace"), agentRoot, shared, Path.Combine(shared, "traces"), Now);

        Assert.True(System.IO.File.Exists(queue));
    }

    [Fact]
    public void Defers_old_copilot_data_while_an_old_session_is_in_use()
    {
        var session = Dir(Path.Combine(OldHome, "session-state"), "live", ageDays: 0);
        File(session, $"inuse.{Environment.ProcessId}.lock", ageDays: 0);
        var database = File(OldHome, "session-store.db", ageDays: 0);
        Dir(OldHome, "arm-cache", ageDays: 0);

        Run(DefaultTrace);

        Assert.True(Directory.Exists(session));
        Assert.True(System.IO.File.Exists(database));
        Assert.False(Directory.Exists(Path.Combine(OldHome, "arm-cache")));
        Assert.Null(ReadState()[AgentDataCleanup.OldLocationsRemovedKey]);
    }

    private void Run(string tracePath, DateTime? now = null)
        => AgentDataCleanup.Run(tracePath, AgentRoot, OldHome, TelemetryQueue, now ?? Now);

    private JsonObject ReadState() => (JsonObject)JsonNode.Parse(System.IO.File.ReadAllText(StatePath))!;

    private static string File(string directory, string name, int ageDays)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        System.IO.File.WriteAllText(path, "x");
        System.IO.File.SetLastWriteTimeUtc(path, Now.AddDays(-ageDays));
        return path;
    }

    private static string Dir(string parent, string name, int ageDays)
    {
        var path = Path.Combine(parent, name);
        Directory.CreateDirectory(path);
        Directory.SetLastWriteTimeUtc(path, Now.AddDays(-ageDays));
        return path;
    }
}
