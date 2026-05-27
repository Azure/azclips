namespace Core.Agent.Tools;

/// <summary>
/// Industry-standard "model-content vs user-display" split for tool results
/// (Gemini CLI <c>llmContent</c>/<c>returnDisplay</c>, Claude Code split).
///
/// Tools that need richer rendering than "ToString() the return value" produce
/// one of these; the event-emitting decorator unwraps it: <see cref="ModelText"/>
/// is passed to the model as the tool result, <see cref="UserDisplay"/> +
/// <see cref="IsError"/> are surfaced via <see cref="ToolCompleted"/>.
/// </summary>
/// <param name="ModelText">Text the LLM receives as tool_result. Plain prose / labeled blocks, not JSON.</param>
/// <param name="IsError">Whether the call's outcome is an error (drives red vs green in renderers).</param>
/// <param name="UserDisplay">Optional one-line human summary. If null, renderers fall back to a trimmed <see cref="ModelText"/>.</param>
public sealed record CirrusToolResult(
    string ModelText,
    bool IsError = false,
    string? UserDisplay = null);
