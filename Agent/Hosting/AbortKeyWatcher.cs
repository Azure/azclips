namespace Core.Agent.Hosting;

/// <summary>
/// Key handling for the thinking view: while a turn runs, Esc and Ctrl+C interrupt
/// the turn and every other key is discarded so it does not land in the next prompt.
/// Keys come from <see cref="ConsoleKeyReader"/>; this type never reads stdin.
/// </summary>
public static class AbortKeyWatcher
{
    /// <summary>
    /// Takes the keys queued now. Returns true at the first key that interrupts the
    /// turn; keys queued after it stay for the next view.
    /// </summary>
    public static bool TakeTurnKeys(ConsoleKeyReader keys)
    {
        for (var remaining = keys.QueuedCount; remaining > 0 && keys.TryRead(out var key); remaining--)
        {
            if (InterruptsTurn(key))
                return true;
        }
        return false;
    }

    /// <summary>True for Esc, and for Ctrl+C when the Ctrl+C order chooses the turn.</summary>
    public static bool InterruptsTurn(ConsoleKeyInfo key)
    {
        if (ConsoleKeyReader.IsEscape(key))
            return true;
        return ConsoleKeyReader.IsCtrlC(key) &&
            ConsoleKeyReader.DecideCtrlC(approvalOpen: false, draftNonEmpty: false, turnRunning: true)
                == CtrlCAction.InterruptTurn;
    }
}
