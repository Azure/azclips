using System.Threading.Channels;

namespace Core.Agent.Hosting;

/// <summary>Source of raw key presses for <see cref="ConsoleKeyReader"/>.</summary>
public interface IConsoleKeySource
{
    /// <summary>Puts the terminal into key mode (Ctrl+C delivered as a key).</summary>
    void EnterKeyMode();

    /// <summary>Blocks until the next key; null when input has ended.</summary>
    ConsoleKeyInfo? ReadKey();

    /// <summary>Restores the terminal mode saved by <see cref="EnterKeyMode"/>.</summary>
    void RestoreMode();
}

/// <summary>What a Ctrl+C press does in the current state.</summary>
public enum CtrlCAction
{
    CancelApproval,
    ClearDraft,
    InterruptTurn,
    Quit,
}

/// <summary>How a line read ended.</summary>
public enum LineReadKind
{
    Submitted,
    EndOfInput,
    Escape,
    CtrlC,
}

public readonly record struct LineRead(LineReadKind Kind, string Text = "");

/// <summary>
/// The only reader of stdin on a real console. A dedicated thread reads keys and
/// queues them; the host loop takes them from the queue and dispatches each key to
/// the active view (thinking, <c>&gt; </c> input, or approval). Waiting for a key
/// never removes it from the queue, so a view that stops waiting loses nothing.
/// </summary>
public sealed class ConsoleKeyReader : IDisposable
{
    private readonly IConsoleKeySource _source;
    private readonly Channel<ConsoleKeyInfo> _keys =
        Channel.CreateUnbounded<ConsoleKeyInfo>(new UnboundedChannelOptions { SingleWriter = true });
    private int _started;
    private int _disposed;

    public ConsoleKeyReader(IConsoleKeySource source)
    {
        _source = source;
    }

    /// <summary>
    /// True when keys can be read from the terminal: stdin is not redirected and
    /// <see cref="Console.SetIn"/> has not replaced the reader captured at startup.
    /// Otherwise input is read line by line from <see cref="Console.In"/>.
    /// </summary>
    public static bool UsesKeyInput(bool inputRedirected, TextReader current, TextReader standardIn) =>
        !inputRedirected && ReferenceEquals(current, standardIn);

    /// <summary>Ctrl+C order: approval, draft, running turn, then quit.</summary>
    public static CtrlCAction DecideCtrlC(bool approvalOpen, bool draftNonEmpty, bool turnRunning)
    {
        if (approvalOpen) return CtrlCAction.CancelApproval;
        if (draftNonEmpty) return CtrlCAction.ClearDraft;
        if (turnRunning) return CtrlCAction.InterruptTurn;
        return CtrlCAction.Quit;
    }

    public static bool IsCtrlC(ConsoleKeyInfo key) =>
        (key.Key == ConsoleKey.C && (key.Modifiers & ConsoleModifiers.Control) != 0) || key.KeyChar == '\x03';

    public static bool IsEscape(ConsoleKeyInfo key) => key.Key == ConsoleKey.Escape;

    /// <summary>Enters key mode and starts the reading thread.</summary>
    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) return;
        _source.EnterKeyMode();
        var thread = new Thread(ReadLoop) { IsBackground = true, Name = "ConsoleKeyReader" };
        thread.Start();
    }

    private void ReadLoop()
    {
        try
        {
            while (Volatile.Read(ref _disposed) == 0)
            {
                var key = _source.ReadKey();
                if (key is null) break;
                _keys.Writer.TryWrite(key.Value);
            }
        }
        catch (InvalidOperationException) { }
        catch (IOException) { }
        _keys.Writer.TryComplete();
    }

    /// <summary>Completes when a key is queued; false when input has ended.</summary>
    public ValueTask<bool> WaitToReadAsync(CancellationToken ct) => _keys.Reader.WaitToReadAsync(ct);

    public bool TryRead(out ConsoleKeyInfo key) => _keys.Reader.TryRead(out key);

    /// <summary>Number of keys queued now.</summary>
    public int QueuedCount => _keys.Reader.Count;

    /// <summary>
    /// Reads one line through <paramref name="editor"/>. Every queued key is applied
    /// before the line is redrawn, so a paste redraws once; Ctrl+C and Esc end the
    /// read wherever they appear and the keys after them stay queued.
    /// </summary>
    public async Task<LineRead> ReadLineAsync(
        LineEditor editor, string prompt, TextWriter output, CancellationToken ct)
    {
        editor.Render(output, prompt);
        while (true)
        {
            if (!await _keys.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                output.WriteLine();
                return new LineRead(LineReadKind.EndOfInput);
            }

            while (_keys.Reader.TryRead(out var key))
            {
                if (IsCtrlC(key)) return new LineRead(LineReadKind.CtrlC, editor.Text);
                if (IsEscape(key)) return new LineRead(LineReadKind.Escape, editor.Text);
                switch (editor.Apply(key))
                {
                    case LineEditResult.Submit:
                        editor.Render(output, prompt);
                        output.WriteLine();
                        return new LineRead(LineReadKind.Submitted, editor.Text);
                    case LineEditResult.EndOfInput:
                        output.WriteLine();
                        return new LineRead(LineReadKind.EndOfInput);
                }
            }

            editor.Render(output, prompt);
        }
    }

    /// <summary>Restores the terminal mode. The reading thread is a background thread.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        if (Volatile.Read(ref _started) != 0)
            _source.RestoreMode();
    }
}

/// <summary>Reads keys from the real console with Ctrl+C delivered as a key.</summary>
public sealed class SystemConsoleKeySource : IConsoleKeySource
{
    private bool _previousTreatControlCAsInput;

    public void EnterKeyMode()
    {
        _previousTreatControlCAsInput = Console.TreatControlCAsInput;
        Console.TreatControlCAsInput = true;
    }

    public ConsoleKeyInfo? ReadKey() => Console.ReadKey(intercept: true);

    public void RestoreMode()
    {
        try { Console.TreatControlCAsInput = _previousTreatControlCAsInput; } catch (IOException) { }
        try { Console.CursorVisible = true; } catch (IOException) { } catch (PlatformNotSupportedException) { }
    }
}
