namespace Core.Agent.Hosting;

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
        ToolStarted t => new[]
        {
            new Segment(AgentColorRole.Muted, "  ▶ "),
            new Segment(AgentColorRole.Default, t.Name, Bold: true),
            new Segment(AgentColorRole.Default, $"({t.ArgsJson})"),
        },

        ToolCompleted t when t.IsError => new[]
        {
            new Segment(AgentColorRole.Error, $"  ✗ {t.Name} "),
            new Segment(AgentColorRole.Muted, $"{t.Elapsed.TotalMilliseconds:F0}ms · "),
            new Segment(AgentColorRole.Default, t.FullResult),
        },
        ToolCompleted t => new[]
        {
            new Segment(AgentColorRole.Muted, $"  ✓ {t.Name} {t.Elapsed.TotalMilliseconds:F0}ms · "),
            new Segment(AgentColorRole.Default, t.FullResult),
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

        SessionErrored e => new[]
        {
            new Segment(AgentColorRole.Error, $"  ✗ session.error [{e.ErrorType}] "),
            new Segment(AgentColorRole.Default, e.Message),
            new Segment(AgentColorRole.Muted,
                BuildErrorMeta(e.StatusCode, e.ErrorCode, e.ProviderCallId)),
        },

        HostLogEmitted h when h.Level == HostLogLevel.Error => new[]
        {
            new Segment(AgentColorRole.Error, "  ✗ [ERROR] "),
            new Segment(AgentColorRole.Default, h.Message),
        },
        HostLogEmitted h => new[]
        {
            new Segment(AgentColorRole.Warning, "  ⚠ [WARN] "),
            new Segment(AgentColorRole.Default, h.Message),
        },

        _ => new[] { new Segment(AgentColorRole.Muted, ev.GetType().Name) },
    };

    private static string BuildErrorMeta(int? statusCode, string? errorCode, string? providerCallId)
    {
        var parts = new List<string>(3);
        if (statusCode is { } sc)         parts.Add(sc.ToString());
        if (!string.IsNullOrEmpty(errorCode))      parts.Add(errorCode);
        if (!string.IsNullOrEmpty(providerCallId)) parts.Add(providerCallId);
        return parts.Count == 0 ? string.Empty : "  · " + string.Join(" · ", parts);
    }
}
