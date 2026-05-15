namespace Core.Agent.Hosting;

/// <summary>
/// Console-backed renderer for the agent event stream. Writes streaming
/// markdown text (via <see cref="OnText"/>) and one-line structured events
/// (via <see cref="OnEvent"/>) to <see cref="Console.Out"/>, using the
/// colors defined in <see cref="AgentEventStyle"/>.
///
/// Pattern:    no host duplication — CLI / TUI agent-mode / future
///             headless agents all point at this one renderer.
/// Avoid:      ❌ rendering inside the SDK callback thread without first
///                breaking the streaming line, or LLM text and the structured
///                line collide on the same row.
/// </summary>
public sealed class ConsoleEventRenderer
{
    // Tracks whether the last write came from streaming-text (no trailing
    // newline) so the next structured line knows to start on a fresh row.
    private bool _lastWasText;

    /// <summary>Streaming markdown chunk from the LLM (no trailing newline).</summary>
    public void OnText(string chunk)
    {
        if (string.IsNullOrEmpty(chunk)) return;
        Console.Write(chunk);
        _lastWasText = true;
    }

    /// <summary>One structured lifecycle event — emitted on its own line.</summary>
    public void OnEvent(AgentEvent ev)
    {
        if (_lastWasText)
        {
            Console.WriteLine();
            _lastWasText = false;
        }

        var (color, text) = AgentEventStyle.Format(ev);
        var prev = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
        }
        finally
        {
            Console.ForegroundColor = prev;
        }
    }
}
