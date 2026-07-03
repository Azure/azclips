namespace Core.Agent.Hosting;

/// <summary>Console implementation of <see cref="IAgentRenderer"/>. Maps roles to 16-color fallback.</summary>
public sealed class ConsoleAgentRenderer : IAgentRenderer
{
    private const string AnsiBold  = "\x1b[1m";
    private const string AnsiReset = "\x1b[22m";

    private readonly object _gate = new();
    private bool _lastWasText;
    private bool _spinnerActive;
    private int _lastSpinnerWidth;
    // Quiet mode (one-shot): don't PRINT the agent-internal scaffolding that would
    // pile up on screen — tool start/complete events, token/timing lines, streaming
    // prose. The spinner still turns and shows the current verb (that's a live line
    // that clears itself, not noise), and errors still print. Set via constructor.
    private readonly bool _quiet;

    public ConsoleAgentRenderer(bool quiet = false) => _quiet = quiet;

    // Set once submit_response has rendered the final outcome. After that the
    // model may still stream a trailing prose paragraph (tokens in flight when
    // the turn is aborted); suppress it so the terminator's render is the last
    // word. Always-on (not gated behind trace) — every session needs this.
    // volatile: written from the submit_response hook thread, read on the
    // streaming thread.
    private volatile bool _finalRendered;

    /// <summary>Call after submit_response renders its outcome. Subsequent OnText is dropped.</summary>
    public void MarkFinalRendered() => _finalRendered = true;

    public void OnText(string chunk)
    {
        if (string.IsNullOrEmpty(chunk)) return;
        if (_finalRendered) return; // suppress trailing prose after the terminator
        if (_quiet) return;         // quiet: streaming prose isn't shown (only the final plan is)
        lock (_gate)
        {
            ClearSpinnerLine();
            Console.Write(chunk);
            _lastWasText = true;
        }
    }

    public void OnEvent(AgentEvent ev)
    {
        // Quiet mode prints only errors; everything else (tool events, token/timing
        // lines) is scaffolding that would clutter the screen. The spinner verb is
        // driven separately (EventRenderer → SpinnerService.SetVerb), so suppressing
        // the print here does NOT stop the spinner from showing what's happening.
        if (_quiet && ev is not SessionErrored
                   && !(ev is HostLogEmitted h && h.Level == HostLogLevel.Error))
            return;

        lock (_gate)
        {
            ClearSpinnerLine();
            if (_lastWasText)
            {
                Console.WriteLine();
                _lastWasText = false;
            }

            var prev = Console.ForegroundColor;
            try
            {
                foreach (var seg in AgentEventStyle.Format(ev))
                {
                    Console.ForegroundColor = Resolve(seg.Role, prev);
                    if (seg.Bold) Console.Write(AnsiBold);
                    Console.Write(seg.Text);
                    if (seg.Bold) Console.Write(AnsiReset);
                }
                Console.WriteLine();
            }
            finally { Console.ForegroundColor = prev; }
        }
    }

    public void OnSpinnerFrame(string frame, string verb)
    {
        lock (_gate)
        {
            if (_lastWasText) return; // don't overwrite mid-stream LLM text
            var line = $"  {frame} {verb}";
            ClearSpinnerLine();
            var prev = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(line);
            }
            finally { Console.ForegroundColor = prev; }
            _lastSpinnerWidth = line.Length;
            _spinnerActive = true;
        }
    }

    public void OnSpinnerStop()
    {
        lock (_gate) { ClearSpinnerLine(); }
    }

    public void RunExclusive(Action action)
    {
        lock (_gate)
        {
            ClearSpinnerLine();
            if (_lastWasText)
            {
                Console.WriteLine();
                _lastWasText = false;
            }
            action();
        }
    }

    private void ClearSpinnerLine()
    {
        if (!_spinnerActive) return;
        Console.Write('\r');
        Console.Write(new string(' ', _lastSpinnerWidth));
        Console.Write('\r');
        _spinnerActive = false;
        _lastSpinnerWidth = 0;
    }

    private static ConsoleColor Resolve(AgentColorRole role, ConsoleColor fallback) => role switch
    {
        AgentColorRole.Default   => fallback,
        AgentColorRole.Muted     => ConsoleColor.DarkGray,
        AgentColorRole.Success   => ConsoleColor.Green,
        AgentColorRole.Warning   => ConsoleColor.Yellow,
        AgentColorRole.Error     => ConsoleColor.Red,
        AgentColorRole.Highlight => ConsoleColor.Cyan,
        _ => fallback,
    };
}
