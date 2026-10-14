using System.Collections.Concurrent;
using System.Diagnostics;
using Core.Abstractions;

namespace Core.Agent.Tools.Resource.Cache;

/// <summary>
/// Process-wide collector for per-tool invocation timing. Tools push a
/// <see cref="ToolInvocation"/> entry on completion; <c>DiagnosisEngine</c>
/// (or any other consumer) reads them after the agent round-trip to break
/// down "LLM thinking" vs "tool work" portions of the wall time.
///
/// Thread-safe; meant to be drained per-DiagnoseAsync call. Call
/// <see cref="DrainAndReset"/> right before invoking the agent to clear
/// stale entries from a previous call.
/// </summary>
public static class ToolInvocationLog
{
    private static readonly ConcurrentQueue<ToolInvocation> _entries = new();

    public static Scope Start(
        string toolName, string? args = null, Func<string, IDisposable?>? activityCreator = null)
    {
        return new Scope(toolName, args, activityCreator);
    }

    public static IReadOnlyList<ToolInvocation> DrainAndReset()
    {
        var list = new List<ToolInvocation>();
        while (_entries.TryDequeue(out var e)) list.Add(e);
        return list;
    }

    public static IReadOnlyList<ToolInvocation> Snapshot()
    {
        return _entries.ToArray();
    }

    /// <summary>
    /// Per-call scope. <see cref="SetResult"/> is optional — when set,
    /// the result string is captured on <see cref="Dispose"/> alongside
    /// timing. Tools that return small structured payloads should call
    /// it; tools that return large blobs may skip it (the consumer can
    /// always read <see cref="Args"/> for context).
    /// </summary>
    public sealed class Scope : IDisposable
    {
        private readonly string _name;
        private readonly string? _args;
        private readonly Stopwatch _sw;
        private readonly IDisposable? _activity;
        private string? _result;
        private int _disposed;

        public Scope(string name, string? args, Func<string, IDisposable?>? activityCreator = null)
        {
            _name = name;
            _args = args;
            _sw = Stopwatch.StartNew();
            _activity = activityCreator?.Invoke($"tool.{name}");
            Activity?.SetTag(TelemetryTags.AgentToolName, name);
        }

        public void SetResult(string result) => _result = result;

        /// <summary>Records a redacted request on the tool span.</summary>
        /// <remarks>Never pass raw model input; each tool defines its own redaction.</remarks>
        public void RecordRequest(string request) => Activity?.SetTag(TelemetryTags.AgentToolRequest, request);

        /// <summary>Records a short, closed-set result on the tool span, such as found or error.</summary>
        public void RecordResponse(string response) => Activity?.SetTag(TelemetryTags.AgentToolResponse, response);

        /// <summary>The tool span, or null when telemetry is off.</summary>
        public Activity? Activity => _activity switch
        {
            Activity activity => activity,
            Core.Abstractions.TelemetryActivityScope wrapped => wrapped.Activity,
            _ => null,
        };

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;
            _sw.Stop();
            _activity?.Dispose();
            _entries.Enqueue(new ToolInvocation(_name, _args, _result, _sw.Elapsed, DateTime.UtcNow));
        }
    }
}

public sealed record ToolInvocation(
    string Name,
    string? Args,
    string? Result,
    TimeSpan Elapsed,
    DateTime CompletedAtUtc);
