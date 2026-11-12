namespace Core.Agent.Hosting;

/// <summary>
/// Drives the braille spinner on an <see cref="IAgentRenderer"/> while the
/// agent is busy. Verb is updated externally as turn state changes (tool
/// start, intent received, etc.).
/// </summary>
public sealed class SpinnerService : IAsyncDisposable
{
    private static readonly string[] Frames =
        { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };

    private readonly IAgentRenderer _renderer;
    private readonly TimeSpan _interval;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private string _verb = "thinking";
    private bool _paused;
    private bool _quiesced;
    private readonly object _frameLock = new();

    public SpinnerService(IAgentRenderer renderer, TimeSpan? interval = null)
    {
        _renderer = renderer;
        _interval = interval ?? TimeSpan.FromMilliseconds(80);
    }

    public void SetVerb(string verb)
    {
        if (!string.IsNullOrWhiteSpace(verb)) _verb = verb;
    }

    public void Start(string initialVerb = "thinking")
    {
        lock (_frameLock) { if (_quiesced) return; }
        Stop();
        _verb = initialVerb;
        _cts = new CancellationTokenSource();
        try { Console.CursorVisible = false; } catch { }
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _loop?.GetAwaiter().GetResult(); } catch { }
        _cts?.Dispose();
        _cts = null;
        _loop = null;
        _paused = false;
        _renderer.OnSpinnerStop();
        try { Console.CursorVisible = true; } catch { }
    }

    /// <summary>
    /// Halt frame emission and clear the spinner line so another writer can
    /// take stdin/stdout exclusively (e.g. the approval prompt's ReadLine).
    /// Idempotent. The loop task keeps running but skips frames.
    /// </summary>
    public void Pause()
    {
        _paused = true;
        _renderer.OnSpinnerStop();
        try { Console.CursorVisible = true; } catch { }
    }

    /// <summary>Resume frame emission with the current verb.</summary>
    public void Resume()
    {
        lock (_frameLock)
        {
            if (_quiesced) return;
            try { Console.CursorVisible = false; } catch { }
            _paused = false;
        }
    }

    /// <summary>
    /// Stops frames for good: after this no frame is drawn and <see cref="Start"/> and
    /// <see cref="Resume"/> do nothing. Does not wait for the loop, so it is safe while
    /// another thread is stuck. Used on process exit before the cursor is restored.
    /// </summary>
    public void Quiesce()
    {
        lock (_frameLock)
        {
            _quiesced = true;
            _paused = true;
            _cts?.Cancel();
        }
        _renderer.OnSpinnerStop();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var i = 0;
        while (!ct.IsCancellationRequested)
        {
            lock (_frameLock)
            {
                if (!_paused)
                {
                    _renderer.OnSpinnerFrame(Frames[i % Frames.Length], _verb);
                    i++;
                }
            }
            try { await Task.Delay(_interval, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        await Task.CompletedTask;
    }

    public static string VerbForTool(string toolName) => toolName switch
    {
        "bash"            => "running bash",
        "powershell"      => "running pwsh",
        "view"            => "reading file",
        "read_file"       => "reading file",
        "write_file"      => "writing file",
        "edit"            => "editing file",
        "grep"            => "grepping",
        "glob"            => "searching files",
        "web_fetch"       => "fetching url",
        "web_search"      => "searching web",
        "propose_plan"    => "planning a fix",
        "submit_response" => "wrapping up",
        _ when toolName.StartsWith("find_")    => "querying ARM",
        _ when toolName.StartsWith("get_")     => "querying ARM",
        _ => $"running {toolName}",
    };
}
