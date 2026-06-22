using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Agent.Hosting;

/// <summary>
/// Decorating <see cref="IAgentRenderer"/> that forwards everything to the inner
/// renderer and writes a structured JSONL trace of the session. Pure diagnostics:
/// trace writes never block or break the inner renderer (forward first, then
/// best-effort Emit). Prose suppression after submit_response lives on
/// <see cref="ConsoleAgentRenderer"/> (always-on), not here.
/// </summary>
public sealed class TraceAgentRenderer : IAgentRenderer, IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IAgentRenderer _inner;
    private readonly StreamWriter? _writer;
    private readonly object _gate = new();
    private long _seq;

    /// <param name="inner">The real renderer (console / TUI) that gets the user-facing output.</param>
    /// <param name="tracePath">Where to write the JSONL trace. Null disables trace writing.</param>
    public TraceAgentRenderer(IAgentRenderer inner, string? tracePath)
    {
        _inner = inner;
        if (!string.IsNullOrWhiteSpace(tracePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tracePath)!);
            _writer = new StreamWriter(new FileStream(tracePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true,
            };
            Emit("trace_start", new { tracePath });
        }
    }

    public void OnText(string chunk)
    {
        if (string.IsNullOrEmpty(chunk)) return;
        _inner.OnText(chunk);
        try { Emit("assistant_text", new { chunk }); } catch { /* trace is diagnostics-only */ }
    }

    public void OnEvent(AgentEvent ev)
    {
        _inner.OnEvent(ev);
        // Serialize against the runtime type, not the AgentEvent base — otherwise
        // System.Text.Json only emits the base's `At` property and drops every
        // concrete field (tool name, args, result, token counts). This is what
        // gives the trace its actual tool I/O.
        try
        {
            if (_writer is not null)
            {
                var payload = JsonSerializer.SerializeToElement(ev, ev.GetType(), JsonOpts);
                EmitRaw("agent_event", ev.GetType().Name, payload);
            }
        }
        catch { /* trace is diagnostics-only */ }
    }

    public void OnSpinnerFrame(string frame, string verb) => _inner.OnSpinnerFrame(frame, verb);

    public void OnSpinnerStop() => _inner.OnSpinnerStop();

    public void RunExclusive(Action action) => _inner.RunExclusive(action);

    /// <summary>Trace-only entry (host-side milestones the IAgentRenderer interface doesn't see).</summary>
    public void Note(string kind, object? payload) => Emit(kind, payload);

    private void Emit(string kind, object? payload)
    {
        if (_writer is null) return;
        lock (_gate)
        {
            _writer.WriteLine(JsonSerializer.Serialize(new
            {
                seq = ++_seq,
                ts = DateTimeOffset.UtcNow.ToString("O"),
                kind,
                payload,
            }, JsonOpts));
        }
    }

    // For agent_event: carries the concrete event type as `event` plus the
    // runtime-typed payload as a pre-serialized JsonElement.
    private void EmitRaw(string kind, string eventType, JsonElement payload)
    {
        if (_writer is null) return;
        lock (_gate)
        {
            _writer.WriteLine(JsonSerializer.Serialize(new
            {
                seq = ++_seq,
                ts = DateTimeOffset.UtcNow.ToString("O"),
                kind,
                @event = eventType,
                payload,
            }, JsonOpts));
        }
    }

    public void Dispose()
    {
        try { Emit("trace_end", null); } catch { }
        _writer?.Dispose();
    }
}
