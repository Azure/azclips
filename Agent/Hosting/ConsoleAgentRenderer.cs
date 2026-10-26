namespace Core.Agent.Hosting;

using System.Text.Json;
using Spectre.Console;

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
    // Verbose (interactive only, agent.interactive.verbose): also surface the
    // args + output of non-shell tools (get_command / find_* / propose_plan),
    // which are otherwise collapsed to a one-line "▶ name" / "✓ name · Nms" so
    // the session isn't flooded by internal scaffolding. Shell tools always show
    // their command + output regardless — that's the result the user cares about.
    private readonly bool _verbose;

    public ConsoleAgentRenderer(bool quiet = false, bool verbose = false)
    {
        _quiet = quiet;
        _verbose = verbose;
    }

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

            // Tool lifecycle events render through Spectre (word-wrap + indent);
            // everything else keeps the flat Segment path. Spectre owns its own
            // trailing newline, so these return without the WriteLine() below.
            if (TryRenderToolEvent(ev))
                return;

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

    // Renders ToolStarted/ToolCompleted/ToolFailed via Spectre with proper
    // wrapping and indentation. Returns false for any other event (caller falls
    // back to the Segment renderer). Display policy:
    //   • shell tools (bash/powershell): always show the command (start) and
    //     output (complete) — that's the user-facing result.
    //   • non-shell tools (get_command / find_* / propose_plan / …): collapse to
    //     a single "▶ name" / "✓ name · Nms" line unless _verbose, in which case
    //     their args + output are shown too (indented, wrapped).
    private bool TryRenderToolEvent(AgentEvent ev)
    {
        switch (ev)
        {
            case ToolStarted t:
            {
                var showDetail = AgentEventStyle.ShowsResult(t.Name) || _verbose;
                AnsiConsole.MarkupLine($"  [grey]▶[/] [bold]{Markup.Escape(t.Name)}[/]");
                if (showDetail)
                    WriteArgsIndented(t.ArgsJson);
                return true;
            }
            case ToolCompleted t when t.IsError:
            {
                AnsiConsole.MarkupLine($"  [red]✗ {Markup.Escape(t.Name)}[/] [grey]{t.Elapsed.TotalMilliseconds:F0}ms[/]");
                WriteOutputIndented(AgentEventStyle.SanitizeToolResult(t.FullResult));
                return true;
            }
            case ToolCompleted t:
            {
                var showDetail = AgentEventStyle.ShowsResult(t.Name) || _verbose;
                AnsiConsole.MarkupLine($"  [grey]✓ {Markup.Escape(t.Name)} {t.Elapsed.TotalMilliseconds:F0}ms[/]");
                if (showDetail)
                    WriteOutputIndented(AgentEventStyle.SanitizeToolResult(t.FullResult));
                return true;
            }
            case ToolFailed t:
            {
                AnsiConsole.MarkupLine($"  [red]✗ {Markup.Escape(t.Name)}[/] [grey]{t.Elapsed.TotalMilliseconds:F0}ms[/]");
                WriteOutputIndented(t.Error);
                return true;
            }
            default:
                return false;
        }
    }

    // args JSON → one "key: value" line per top-level field, indented. Falls back
    // to raw indented text if the args aren't a JSON object (some tools pass a
    // bare string). Values are Spectre-escaped and wrap to the console width.
    private static void WriteArgsIndented(string argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson))
            return;
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    var v = p.Value.ValueKind == JsonValueKind.String
                        ? p.Value.GetString() ?? string.Empty
                        : p.Value.ToString();
                    AnsiConsole.MarkupLine($"    [grey]{Markup.Escape(p.Name)}:[/] {Markup.Escape(v)}");
                }
                return;
            }
            // Some tools pass a bare string as their args (SDK emits "..." for a
            // string-typed argument). Render the decoded value, not the raw
            // quoted/escaped JSON literal.
            if (doc.RootElement.ValueKind == JsonValueKind.String)
            {
                WriteOutputIndented(doc.RootElement.GetString() ?? string.Empty);
                return;
            }
        }
        catch (JsonException) { /* not JSON — fall through to raw */ }
        WriteOutputIndented(argsJson);
    }

    // Multi-line tool output, each line indented under the event. Spectre wraps
    // long lines to the console width instead of hard-cutting mid-token.
    private static void WriteOutputIndented(string output)
    {
        if (string.IsNullOrEmpty(output))
            return;
        foreach (var line in output.Split('\n'))
            AnsiConsole.MarkupLine($"    {Markup.Escape(line.TrimEnd('\r'))}");
    }

    public void OnSpinnerFrame(string frame, string verb)
    {
        if (Console.IsOutputRedirected) return; // no spinner frames in redirected stdout
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
