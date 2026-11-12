namespace Core.Agent.AgentMode;

/// <summary>
/// The Ctrl+C handler shared by <see cref="Console.CancelKeyPress"/> and the key watcher.
/// Follows System.CommandLine ProcessTerminationHandler: record the press, request cancellation,
/// and on the first press arm one timer that forces the exit if the session does not unwind.
/// </summary>
internal sealed class CtrlCController
{
    internal static readonly TimeSpan ForcedExitDelay = TimeSpan.FromSeconds(2);
    internal const int CancelledExitCode = 130;

    private readonly CancellationTokenSource _cts;
    private readonly TimeProvider _timeProvider;
    private readonly Func<Task> _cancelAsync;
    private readonly TaskCompletionSource<int> _forcedExit = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _requested;
    private int _armed;
    private ITimer? _timer;

    /// <param name="cancelAsync">Cancels the session; by default cancels <paramref name="cts"/>.</param>
    internal CtrlCController(CancellationTokenSource cts, TimeProvider timeProvider, Func<Task>? cancelAsync = null)
    {
        _cts = cts;
        _timeProvider = timeProvider;
        _cancelAsync = cancelAsync ?? (() => cts.CancelAsync());
    }

    /// <summary>True once Ctrl+C was pressed or the session token was cancelled.</summary>
    public bool SessionCancelled => Volatile.Read(ref _requested) || _cts.IsCancellationRequested;

    /// <summary>True once Ctrl+C was pressed; the exit decision maps it to 130.</summary>
    public bool Requested => Volatile.Read(ref _requested);

    /// <summary>Completes with 130 when the session has not exited 2 s after the first Ctrl+C.</summary>
    public Task<int> ForcedExit => _forcedExit.Task;

    public void OnCtrlC()
    {
        Volatile.Write(ref _requested, true);
        _ = CancelAsync();
        if (Interlocked.Exchange(ref _armed, 1) == 0)
            _timer = _timeProvider.CreateTimer(
                _ => _forcedExit.TrySetResult(CancelledExitCode), null, ForcedExitDelay, Timeout.InfiniteTimeSpan);
    }

    private async Task CancelAsync()
    {
        try { await _cancelAsync().ConfigureAwait(false); }
        catch { Console.Error.WriteLine("Agent telemetry: cancellation_callback_failed"); }
    }
}
