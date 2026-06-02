using System.Diagnostics.CodeAnalysis;

namespace Core.Agent.Hosting;

/// <summary>
/// During a busy agent turn, watches stdin for Esc / Ctrl+C and swallows every
/// other keypress so the user can't queue up garbage that lands in the next
/// prompt. Esc aborts the current turn (calls <c>_onAbort</c>); Ctrl+C quits
/// the whole session (calls <c>_onQuit</c>). Started right before
/// <c>SendAsync</c> and stopped the instant the turn ends (or an approval
/// needs to read y/n via <see cref="Console.ReadLine"/>).
///
/// Pattern:    cooperative polling on <see cref="Console.KeyAvailable"/> so we
///             never sit inside a blocking <see cref="Console.ReadKey(bool)"/>
///             — that would fight <c>ConsoleApprovalPrompt</c> for stdin.
///             When the watcher's CTS is cancelled, the loop exits between
///             polls and stdin is immediately free for ReadLine.
/// Avoid:      ❌ blocking ReadKey on a background thread (you can't cancel a
///                blocking ReadKey on Windows without P/Invoke, and it would
///                race the approval's ReadLine);
///             ❌ doing this from the SDK callback thread (stdin must have
///                exactly one reader — see <see cref="ConsoleApprovalPrompt"/>);
///             ❌ leaving Ctrl+C reliance on <c>Console.CancelKeyPress</c>
///                alone — that fires asynchronously and the SDK's turn keeps
///                streaming for seconds before the daemon's cancellation hits.
///                Calling <c>AbortAsync</c> proactively cuts the turn instantly.
///
/// Redirected stdin (e.g. piped input, CI): <see cref="Console.KeyAvailable"/>
/// throws <see cref="InvalidOperationException"/>. We detect that on the first
/// probe and become a no-op so non-TTY hosts (test harnesses, scripts) aren't
/// broken — Ctrl+C still works via the runtime's signal handler in those
/// environments.
/// </summary>
public sealed class AbortKeyWatcher
{
    private readonly Func<CancellationToken, Task> _onAbort;
    private readonly Action? _onQuit;
    private readonly TimeSpan _pollInterval;
    private readonly ManualResetEventSlim _gate = new(initialState: true);

    public AbortKeyWatcher(
        Func<CancellationToken, Task> onAbort,
        Action? onQuit = null,
        TimeSpan? pollInterval = null)
    {
        _onAbort = onAbort;
        _onQuit = onQuit;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(50);
    }

    /// <summary>
    /// Suspend polling so another reader (the approval pump) can own stdin
    /// without contention. Any keystrokes typed while paused stay in the OS
    /// input buffer and are visible to whoever is reading instead.
    /// </summary>
    public void Pause() => _gate.Reset();

    /// <summary>Resume polling after <see cref="Pause"/>.</summary>
    public void Resume() => _gate.Set();

    /// <summary>
    /// Run the watch loop until <paramref name="ct"/> is cancelled or the user
    /// hits an abort key. Returns true if an abort fired (caller may want to
    /// print a "↯ aborted" line), false on normal cancellation.
    /// </summary>
    public async Task<bool> RunAsync(CancellationToken ct)
    {
        if (!TryProbeStdin())
        {
            // Non-TTY: sleep until cancelled; runtime SIGINT handler still works.
            try { await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            return false;
        }

        while (!ct.IsCancellationRequested)
        {
            try { _gate.Wait(ct); }
            catch (OperationCanceledException) { return false; }

            if (!Console.KeyAvailable)
            {
                try { await Task.Delay(_pollInterval, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return false; }
                continue;
            }

            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.C && (key.Modifiers & ConsoleModifiers.Control) != 0)
            {
                _onQuit?.Invoke();
                return true;
            }

            if (key.Key == ConsoleKey.Escape)
            {
                try { await _onAbort(ct).ConfigureAwait(false); } catch { }
                return true;
            }
        }

        return false;
    }

    [SuppressMessage("Design", "CA1031", Justification = "Probe is intentionally swallow-all.")]
    private static bool TryProbeStdin()
    {
        try { _ = Console.KeyAvailable; return true; }
        catch { return false; }
    }
}
