namespace Core.Agent.Hosting;

/// <summary>
/// Single-line renderer mapping an <see cref="AgentEvent"/> to a console color
/// plus its display text. Pulled out of the host renderers so CLI / TUI /
/// future hosts can share the same color + format choices (and so the snapshot
/// tests in <c>tests/Core.Agent.Tests</c> can lock both axes down).
///
/// Pattern:    pure function (event → (color, text)); IO side-effects belong
///             to <see cref="ConsoleEventRenderer"/>.
/// Avoid:      ❌ duplicating these mappings in CLI and TUI hosts (the old
///                spike had two RenderAgentEvent copies that drifted on the
///                LlmTurnCompleted line and the run_command failure color).
/// </summary>
public static class AgentEventStyle
{
    /// <summary>Returns the console color + formatted single-line text for an event.</summary>
    public static (ConsoleColor Color, string Text) Format(AgentEvent ev) => ev switch
    {
        ToolStarted t            => (ConsoleColor.Cyan,     $"  ▶ {t.Name}({Trim(t.ArgsJson, 80)})"),
        ToolCompleted t          => (t.IsError ? ConsoleColor.Red : ConsoleColor.Green,
                                     $"  {(t.IsError ? "✗" : "✓")} {t.Name} {t.Elapsed.TotalMilliseconds:F0}ms · {Trim(t.FullResult, 120)}"),
        ToolFailed t             => (ConsoleColor.Red,
                                     $"  ✗ {t.Name} {t.Elapsed.TotalMilliseconds:F0}ms · {Trim(t.Error, 80)}"),
        SubAgentInvokeStarted s  => (ConsoleColor.Magenta,
                                     $"  ⊳ sub-agent {s.AgentName}({Trim(s.Query, 80)})"),
        SubAgentInvokeCompleted s=> (ConsoleColor.Magenta,
                                     $"  ⊲ sub-agent {s.AgentName} {s.Elapsed.TotalMilliseconds:F0}ms · {Trim(s.Response, 80)}"),
        SubAgentInvokeFailed s   => (ConsoleColor.Red,
                                     $"  ⊲ sub-agent {s.AgentName} {s.Elapsed.TotalMilliseconds:F0}ms · ✗ {Trim(s.Error, 80)}"),
        LlmFirstToken e          => (ConsoleColor.DarkGray,
                                     $"  ▶ LLM streaming · TTFT {e.TimeToFirstToken.TotalSeconds:F1}s"),
        LlmTurnCompleted e       => (ConsoleColor.DarkGray,
                                     $"  ✓ LLM turn {e.Elapsed.TotalSeconds:F1}s · in {e.InputTokens} · out {e.OutputTokens} · cached {e.CachedTokens}"),
        _                        => (ConsoleColor.Gray, ev.GetType().Name),
    };

    private static string Trim(string s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length > max ? s[..max] + "…" : s);
}
