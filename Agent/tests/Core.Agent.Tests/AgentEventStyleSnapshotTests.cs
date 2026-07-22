using Core.Agent.Hosting;
using Core.Agent.Tests.SnapshotTesting;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Snapshot tests for <see cref="AgentEventStyle.Format"/>: the pure
/// (event → styled segments) layer shared by every host renderer. Each
/// segment is rendered as <c>[Role|bold]"text"</c> so a diff fails on any
/// role / bold / text drift.
/// </summary>
public class AgentEventStyleSnapshotTests
{
    private static readonly DateTimeOffset At = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToolStarted_PrefixIsDarkGray_NameIsBoldDefault_ArgsAreDefault()
    {
        var ev = new ToolStarted(
            Name: "powershell",
            ArgsJson: "{\"command\":\"az group list\"}",
            CallId: "call1", ParentCallId: null, At: At);

        SnapshotAssert.Matches("style-tool-started", RenderSegments(ev));
    }

    [Fact]
    public void ToolCompleted_Success_PrefixDarkGray_ResultDefault()
    {
        var ev = new ToolCompleted(
            Name: "powershell",
            Elapsed: TimeSpan.FromMilliseconds(123),
            FullResult: "SUCCESS · exit 0\noutput: rg1, rg2",
            IsError: false,
            CallId: "call1", ParentCallId: null, At: At);

        SnapshotAssert.Matches("style-tool-completed-success", RenderSegments(ev));
    }

    [Fact]
    public void ToolCompleted_IsError_NameRed_ElapsedDarkGray_ResultDefault()
    {
        var ev = new ToolCompleted(
            Name: "powershell",
            Elapsed: TimeSpan.FromMilliseconds(45),
            FullResult: "FAILURE · exit 1 · ResourceNotFound",
            IsError: true,
            CallId: "call1", ParentCallId: null, At: At);

        SnapshotAssert.Matches("style-tool-completed-iserror", RenderSegments(ev));
    }

    [Fact]
    public void ToolFailed_NameRed_ElapsedDarkGray_ErrorRed()
    {
        var ev = new ToolFailed(
            Name: "powershell",
            Elapsed: TimeSpan.FromMilliseconds(78),
            Error: "AuthenticationFailed: token expired",
            CallId: "call1", ParentCallId: null, At: At);

        SnapshotAssert.Matches("style-tool-failed", RenderSegments(ev));
    }

    [Fact]
    public void SubAgent_Started_PrefixDarkGray_NameBold()
    {
        var ev = new SubAgentInvokeStarted(
            AgentName: "diagnose",
            Query: "vm not reachable",
            CallId: "call1", ParentCallId: null, At: At);

        SnapshotAssert.Matches("style-subagent-started", RenderSegments(ev));
    }

    [Fact]
    public void SubAgent_Completed_PrefixDarkGray_ResponseDefault()
    {
        var ev = new SubAgentInvokeCompleted(
            AgentName: "diagnose",
            Elapsed: TimeSpan.FromSeconds(2.5),
            Response: "checked NSG + route table; NSG blocks 22",
            CallId: "call1", ParentCallId: null, At: At);

        SnapshotAssert.Matches("style-subagent-completed", RenderSegments(ev));
    }

    [Fact]
    public void SubAgent_Failed_NameRed_ErrorRed()
    {
        var ev = new SubAgentInvokeFailed(
            AgentName: "diagnose",
            Elapsed: TimeSpan.FromSeconds(1.0),
            Error: "subagent crashed: NullReferenceException",
            CallId: "call1", ParentCallId: null, At: At);

        SnapshotAssert.Matches("style-subagent-failed", RenderSegments(ev));
    }

    [Fact]
    public void Llm_FirstToken_IsDarkGray()
    {
        var ev = new LlmFirstToken(
            TimeToFirstToken: TimeSpan.FromMilliseconds(1500),
            At: At);

        SnapshotAssert.Matches("style-llm-first-token", RenderSegments(ev));
    }

    [Fact]
    public void Llm_TurnCompleted_IsDarkGray()
    {
        var ev = new LlmTurnCompleted(
            InputTokens: 1500, OutputTokens: 42, CachedTokens: 1200,
            Elapsed: TimeSpan.FromSeconds(3.4), At: At);

        SnapshotAssert.Matches("style-llm-turn-completed", RenderSegments(ev));
    }

    [Fact]
    public void SessionErrored_MarkerRed_MessageDefault_MetaDarkGray()
    {
        var ev = new SessionErrored(
            ErrorType: "rate_limit",
            Message: "You have exceeded the model rate limit. Retry in 30s.",
            ErrorCode: "user_model_rate_limited",
            StatusCode: 429,
            ProviderCallId: "req-abc123",
            At: At);

        SnapshotAssert.Matches("style-session-errored", RenderSegments(ev));
    }

    [Fact]
    public void SessionErrored_NoOptionalFields_OmitsMetaLine()
    {
        var ev = new SessionErrored(
            ErrorType: "authentication",
            Message: "GitHub token has expired.",
            ErrorCode: null,
            StatusCode: null,
            ProviderCallId: null,
            At: At);

        SnapshotAssert.Matches("style-session-errored-minimal", RenderSegments(ev));
    }

    /// <summary>
    /// Serializes the segment list into a deterministic, human-readable form.
    /// Format per segment: <c>[color|bold]"text"</c> where <c>color</c> is
    /// the <see cref="ConsoleColor"/> name or <c>default</c> for unset, and
    /// <c>|bold</c> appears only when bold is true. Each segment on its own
    /// line; trailing newline omitted so equality is exact.
    /// </summary>
    private static string RenderSegments(AgentEvent ev)
    {
        var segs = AgentEventStyle.Format(ev);
        var lines = segs.Select(s =>
        {
            var role = s.Role.ToString();
            var bold = s.Bold ? "|bold" : "";
            var text = s.Text.Replace("\\", "\\\\").Replace("\"", "\\\"")
                              .Replace("\r", "\\r").Replace("\n", "\\n");
            return $"[{role}{bold}]\"{text}\"";
        });
        return string.Join("\n", lines);
    }
}
