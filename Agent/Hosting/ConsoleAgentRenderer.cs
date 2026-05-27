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

    public void OnText(string chunk)
    {
        if (string.IsNullOrEmpty(chunk)) return;
        lock (_gate)
        {
            ClearSpinnerLine();
            Console.Write(chunk);
            _lastWasText = true;
        }
    }

    public void OnEvent(AgentEvent ev)
    {
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
