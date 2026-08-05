namespace Core.Agent.Hosting;

using System.Text.RegularExpressions;

/// <summary>
/// Pure mapping from <see cref="AgentEvent"/> to a list of styled text segments,
/// shared by every host renderer (console / TUI / future web). Colors are
/// expressed as semantic <see cref="AgentColorRole"/>s; the renderer resolves
/// them to concrete terminal / palette values.
/// </summary>
public static class AgentEventStyle
{
    public readonly record struct Segment(AgentColorRole Role, string Text, bool Bold = false);

    public static IReadOnlyList<Segment> Format(AgentEvent ev) => ev switch
    {
        // Show args only for shell tools (the command is what matters). Other
        // tools' args are internal — propose_plan's args are the entire plan JSON,
        // which would dump the whole plan twice. Just name the tool.
        ToolStarted t when ShowsResult(t.Name) => new[]
        {
            new Segment(AgentColorRole.Muted, "  ▶ "),
            new Segment(AgentColorRole.Default, t.Name, Bold: true),
            new Segment(AgentColorRole.Default, $"({t.ArgsJson})"),
        },
        ToolStarted t => new[]
        {
            new Segment(AgentColorRole.Muted, "  ▶ "),
            new Segment(AgentColorRole.Default, t.Name, Bold: true),
        },

        ToolCompleted t when t.IsError => new[]
        {
            new Segment(AgentColorRole.Error, $"  ✗ {t.Name} "),
            new Segment(AgentColorRole.Muted, $"{t.Elapsed.TotalMilliseconds:F0}ms · "),
            new Segment(AgentColorRole.Default, SanitizeToolResult(t.FullResult)),
        },
        // Successful tool: only shell tools (bash/powershell) show their output —
        // that's the command result the user wants. Query/plan tools (get_command,
        // list_commands, find_*, propose_plan, …) are agent-internal scaffolding;
        // in the inline console their raw result just floods the screen, so we
        // show only the "✓ name · Nms" line. (A TUI would collapse these instead.)
        ToolCompleted t when ShowsResult(t.Name) => new[]
        {
            new Segment(AgentColorRole.Muted, $"  ✓ {t.Name} {t.Elapsed.TotalMilliseconds:F0}ms · "),
            new Segment(AgentColorRole.Default, SanitizeToolResult(t.FullResult)),
        },
        ToolCompleted t => new[]
        {
            new Segment(AgentColorRole.Muted, $"  ✓ {t.Name} {t.Elapsed.TotalMilliseconds:F0}ms"),
        },

        ToolFailed t => new[]
        {
            new Segment(AgentColorRole.Error, $"  ✗ {t.Name} "),
            new Segment(AgentColorRole.Muted, $"{t.Elapsed.TotalMilliseconds:F0}ms · "),
            new Segment(AgentColorRole.Error, t.Error),
        },

        SubAgentInvokeStarted s => new[]
        {
            new Segment(AgentColorRole.Muted, "  ⊳ sub-agent "),
            new Segment(AgentColorRole.Default, s.AgentName, Bold: true),
            new Segment(AgentColorRole.Default, $"({s.Query})"),
        },
        SubAgentInvokeCompleted s => new[]
        {
            new Segment(AgentColorRole.Muted, $"  ⊲ sub-agent {s.AgentName} {s.Elapsed.TotalMilliseconds:F0}ms · "),
            new Segment(AgentColorRole.Default, s.Response),
        },
        SubAgentInvokeFailed s => new[]
        {
            new Segment(AgentColorRole.Error, $"  ⊲ sub-agent {s.AgentName} "),
            new Segment(AgentColorRole.Muted, $"{s.Elapsed.TotalMilliseconds:F0}ms · "),
            new Segment(AgentColorRole.Error, s.Error),
        },

        LlmFirstToken e => new[]
        {
            new Segment(AgentColorRole.Muted, $"  ▶ LLM streaming · TTFT {e.TimeToFirstToken.TotalSeconds:F1}s"),
        },
        LlmTurnCompleted e => new[]
        {
            new Segment(AgentColorRole.Muted,
                $"  ✓ LLM turn {e.Elapsed.TotalSeconds:F1}s · in {e.InputTokens} · out {e.OutputTokens} · cached {e.CachedTokens}"),
        },

        SessionErrored e => BuildSessionErroredSegments(e),

        HostLogEmitted h when h.Level == HostLogLevel.Error => new[]
        {
            new Segment(AgentColorRole.Error, "  ✗ ERROR: "),
            new Segment(AgentColorRole.Default, h.Message),
        },
        HostLogEmitted h => new[]
        {
            new Segment(AgentColorRole.Warning, "  ⚠ WARNING: "),
            new Segment(AgentColorRole.Default, h.Message),
        },

        _ => new[] { new Segment(AgentColorRole.Muted, ev.GetType().Name) },
    };

    private static IReadOnlyList<Segment> BuildSessionErroredSegments(SessionErrored e)
    {
        var meta = BuildErrorMeta(e.StatusCode, e.ErrorCode, e.ProviderCallId);
        if (meta.Length == 0)
        {
            return new[]
            {
                new Segment(AgentColorRole.Error, $"  ✗ session.error [{e.ErrorType}] "),
                new Segment(AgentColorRole.Default, e.Message),
            };
        }
        return new[]
        {
            new Segment(AgentColorRole.Error, $"  ✗ session.error [{e.ErrorType}] "),
            new Segment(AgentColorRole.Default, e.Message),
            new Segment(AgentColorRole.Muted, meta),
        };
    }

    private static string BuildErrorMeta(int? statusCode, string? errorCode, string? providerCallId)
    {
        var parts = new List<string>(3);
        if (statusCode is { } sc)         parts.Add(sc.ToString());
        if (!string.IsNullOrEmpty(errorCode))      parts.Add(errorCode);
        if (!string.IsNullOrEmpty(providerCallId)) parts.Add(providerCallId);
        return parts.Count == 0 ? string.Empty : "  · " + string.Join(" · ", parts);
    }

    // Strips terminal control noise from tool output (notably progress spinners
    // from az CLI / knack that draw via \b backspaces and \r returns). Without
    // this, piped stdout captures every frame and the renderer prints them all
    // in sequence: "\ Running .. | Running .. / Running .. (repeat N times)".
    //
    // Whether a successful tool's result should be printed inline. Only the shell
    // tools produce output the user wants to read; everything else is internal
    // scaffolding (get_command, find_*, propose_plan, …) whose result would just
    // flood the console. internal for direct unit testing (ShowsResultTests).
    internal static bool ShowsResult(string toolName) =>
        string.Equals(toolName, "bash",       StringComparison.OrdinalIgnoreCase) ||
        string.Equals(toolName, "powershell", StringComparison.OrdinalIgnoreCase);

    //   1. Drop ANSI escape sequences.
    //   2. Drop \r-runs and \b-runs (they were meant to overwrite, not stack).
    //   3. Collapse consecutive identical lines (spinner frames repeat).
    // Output is NOT length-capped: this is display-only (never fed to the model —
    // that path uses the SDK's raw tool result), and the terminal has scrollback,
    // so truncating here only risked dropping the head of a long result.
    private static readonly Regex AnsiEscape = new("\x1b\\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);
    private static readonly Regex OverwriteRuns = new("[\r\b]+[^\r\b\n]*", RegexOptions.Compiled);

    internal static string SanitizeToolResult(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;

        string cleaned = AnsiEscape.Replace(raw, string.Empty);
        cleaned = OverwriteRuns.Replace(cleaned, string.Empty);

        var lines = cleaned.Split('\n');
        var dedup = new List<string>(lines.Length);
        string? prev = null;
        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed == prev) continue;
            dedup.Add(trimmed);
            prev = trimmed;
        }
        return string.Join('\n', dedup);
    }
}
