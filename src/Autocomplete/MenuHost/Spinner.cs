using System.Text;

namespace Cirrus.Autocomplete.MenuHost;

/// <summary>
/// Lightweight spinner that writes to /dev/tty (so it's visible while bash captures stdout for COMPREPLY).
/// Same braille frames used by AgentEngine to keep the user experience consistent.
///
/// Usage:
///   await using var spinner = Spinner.Start("Thinking...");
///   ... do work ...
///   await spinner.StopAsync(); // or rely on DisposeAsync
/// </summary>
internal sealed class Spinner : IAsyncDisposable
{
    private static readonly string[] Frames =
    {
        "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏",
    };

    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private readonly FileStream? _tty;
    private readonly StreamWriter? _writer;
    private readonly string _label;
    private int _stopped;

    private Spinner(string label)
    {
        _label = label;

        try
        {
            _tty = new FileStream("/dev/tty", FileMode.Open, FileAccess.Write);
            _writer = new StreamWriter(_tty, Encoding.UTF8) { AutoFlush = true };
        }
        catch
        {
            // No tty (e.g., piped). Fall back to no-op loop.
            _tty = null;
            _writer = null;
        }

        _loop = Task.Run(RunAsync);
    }

    public static Spinner Start(string label = "") => new(label);

    private async Task RunAsync()
    {
        if (_writer == null)
        {
            return;
        }

        // Save cursor, move to next line, hide cursor.
        // The spinner draws on a brand-new line below the prompt so it does
        // not overwrite what the user has already typed.
        _writer.Write("\u001b7\r\n\u001b[?25l");

        int i = 0;
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                _writer.Write(string.IsNullOrEmpty(_label)
                    ? $"\r\u001b[K{Frames[i++ % Frames.Length]}"
                    : $"\r\u001b[K{Frames[i++ % Frames.Length]} {_label}");
                try
                {
                    await Task.Delay(100, _cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            // Clear the spinner line, restore cursor to original position, show it.
            try
            {
                _writer.Write("\r\u001b[K\u001b8\u001b[?25h");
            }
            catch
            {
                // tty may be gone; ignore.
            }
        }
    }

    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 1)
        {
            return;
        }

        _cts.Cancel();
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch
        {
            // Spinner is best-effort; do not surface its errors.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _cts.Dispose();
        _writer?.Dispose();
        _tty?.Dispose();
    }
}
