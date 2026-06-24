namespace Core.Agent;

/// <summary>
/// Terminal/console-environment checks reused across the agent (the approval
/// prompt's color gating today; mode selection and the agent fallback later).
/// Agent-scoped on purpose — the classic CLI keeps its own inline guard.
/// </summary>
public static class ConsoleEnvironment
{
    /// <summary>
    /// True when the process is attached to an interactive terminal (a TTY) on
    /// BOTH stdin and stdout — i.e., it can prompt the user and safely emit
    /// color / ANSI escapes. False under CI, a pipe, redirected stdin/stdout, or
    /// any non-interactive host. Gate user prompts and color output on this so
    /// unattended runs stay clean and never block on input.
    /// </summary>
    public static bool IsInteractive =>
        !Console.IsInputRedirected && !Console.IsOutputRedirected;
}
