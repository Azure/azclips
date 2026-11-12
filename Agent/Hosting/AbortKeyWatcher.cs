using System.Diagnostics.CodeAnalysis;

namespace Core.Agent.Hosting;

/// <summary>The console key calls <see cref="AbortKeyWatcher"/> makes.</summary>
internal interface IConsoleKeys
{
    /// <summary>Throws <see cref="InvalidOperationException"/> when stdin is redirected, as Console does.</summary>
    bool KeyAvailable { get; }
    ConsoleKeyInfo ReadKey(bool intercept);
}

internal sealed class SystemConsoleKeys : IConsoleKeys
{
    internal static readonly SystemConsoleKeys Instance = new();
    public bool KeyAvailable => Console.KeyAvailable;
    public ConsoleKeyInfo ReadKey(bool intercept) => Console.ReadKey(intercept);
}

/// <summary>
/// During a busy agent turn, watches stdin for Esc / Ctrl+C and swallows every
/// other keypress so the user can't queue up garbage that lands in the next
/// prompt. Esc aborts the current turn (calls <c>_onAbort</c>); Ctrl+C quits
/// the whole session (calls <c>_onQuit</c>).
///
/// Pattern:    cooperative polling on KeyAvailable, owned by one caller: the turn pump starts
///             <see cref="RunAsync"/>, and before anything else reads stdin it cancels the run and
///             awaits it (PSReadLine ReadLine.cs signal-then-Join). The token is checked again
///             between KeyAvailable and ReadKey, so a stopped watcher never reads a key.
///             An Esc while no turn runs is discarded (Codex bottom_pane is_task_running).
/// Avoid:      ❌ blocking ReadKey on a background thread (you can't cancel a
///                blocking ReadKey on Windows without P/Invoke, and it would
///                race the approval's ReadLine);
///             ❌ doing this from the SDK callback thread (stdin must have
///                exactly one reader — see <see cref="ConsoleApprovalPrompt"/>).
///
/// Redirected stdin (e.g. piped input, CI): KeyAvailable throws
/// <see cref="InvalidOperationException"/>. We detect that on the first
/// probe and become a no-op so non-TTY hosts (test harnesses, scripts) aren't
/// broken — Ctrl+C still works via the runtime's signal handler in those
/// environments.
/// </summary>
public sealed class AbortKeyWatcher
{
    private readonly Func<CancellationToken, Task> _onAbort;
    private readonly Action? _onQuit;
    private readonly TimeSpan _pollInterval;
    private readonly IConsoleKeys _keys;
    private readonly TimeProvider _timeProvider;

    public AbortKeyWatcher(
        Func<CancellationToken, Task> onAbort,
        Action? onQuit = null,
        TimeSpan? pollInterval = null)
        : this(onAbort, onQuit, pollInterval, SystemConsoleKeys.Instance, TimeProvider.System)
    {
    }

    internal AbortKeyWatcher(
        Func<CancellationToken, Task> onAbort, Action? onQuit, TimeSpan? pollInterval,
        IConsoleKeys keys, TimeProvider timeProvider)
    {
        _onAbort = onAbort;
        _onQuit = onQuit;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(50);
        _keys = keys;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Run the watch loop until <paramref name="ct"/> is cancelled or the user hits an abort key.
    /// Returns true for Ctrl+C, or for an Esc that aborted a running turn; false on cancellation.
    /// </summary>
    /// <param name="isTurnRunning">False once the turn has finished; an Esc then is discarded.</param>
    public async Task<bool> RunAsync(CancellationToken ct, Func<bool>? isTurnRunning = null)
    {
        if (ct.IsCancellationRequested || !TryProbeStdin())
        {
            // Non-TTY: sleep until cancelled; runtime SIGINT handler still works.
            try { await Task.Delay(Timeout.InfiniteTimeSpan, _timeProvider, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            return false;
        }

        while (true)
        {
            if (ct.IsCancellationRequested) return Drain();
            if (!_keys.KeyAvailable)
            {
                try { await Task.Delay(_pollInterval, _timeProvider, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return Drain(); }
                continue;
            }
            if (ct.IsCancellationRequested) return Drain();

            var key = _keys.ReadKey(intercept: true);
            if (IsCtrlC(key))
            {
                _onQuit?.Invoke();
                return true;
            }

            if (key.Key == ConsoleKey.Escape && (isTurnRunning?.Invoke() ?? true))
            {
                try { await _onAbort(ct).ConfigureAwait(false); } catch { }
                return true;
            }
        }
    }

    // Consumes keys typed during the turn so they do not reach the next prompt. Ctrl+C still quits.
    [SuppressMessage("Design", "CA1031", Justification = "Drain is best effort.")]
    private bool Drain()
    {
        try
        {
            while (_keys.KeyAvailable)
            {
                if (IsCtrlC(_keys.ReadKey(intercept: true)))
                {
                    _onQuit?.Invoke();
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    private static bool IsCtrlC(ConsoleKeyInfo key) =>
        key.Key == ConsoleKey.C && (key.Modifiers & ConsoleModifiers.Control) != 0;

    [SuppressMessage("Design", "CA1031", Justification = "Probe is intentionally swallow-all.")]
    private bool TryProbeStdin()
    {
        try { _ = _keys.KeyAvailable; return true; }
        catch { return false; }
    }
}
