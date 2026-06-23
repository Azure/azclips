namespace Core.Abstractions;

/// <summary>
/// Shared terminal/console-environment checks used across the CLI, TUI, and agent.
/// </summary>
public static class ConsoleEnvironment
{
    /// <summary>
    /// True when the process is attached to an interactive terminal (a TTY) on
    /// BOTH stdin and stdout — i.e., it can prompt the user and safely emit
    /// color / ANSI escapes. False under CI, a pipe, redirected stdin/stdout, or
    /// any non-interactive host. Gate user prompts, the agent fallback, and color
    /// output on this so unattended runs stay clean and never block on input.
    /// </summary>
    public static bool IsInteractive =>
        !Console.IsInputRedirected && !Console.IsOutputRedirected;
}
