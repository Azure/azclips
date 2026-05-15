using System.Diagnostics;
using System.Text;
using Core.Agent.Models;
using Core.Agent.Tools;

namespace Core.Agent.Hosting;

/// <summary>
/// Generic "spawn a child process, tee stdout/stderr live to the console while
/// also buffering them for the agent" implementation of <see cref="RunCommandTool.RunCommandExecutor"/>.
/// The host supplies the exe path to launch (<paramref name="exePath"/>) and an
/// optional argument-rewriter; everything else (stdio inheritance, ANSI colors,
/// ExitCode → ExecutionOutcome mapping) is identical regardless of host.
///
/// Pattern:    inject the host-specific bit (exe path), keep the spawn /
///             stream / outcome plumbing in one place.
/// Avoid:      ❌ inheriting stdin (lets the child steal keystrokes from the
///                parent's approval prompt — the spike's recurring crash);
///             ❌ rendering stderr in red unconditionally — many CLI tools
///                (git, npm) emit informational lines on stderr.
/// </summary>
public sealed class ProcessExecutor
{
    private readonly string _exePath;
    private readonly Func<string, string> _rewriteCommand;

    /// <summary>
    /// </summary>
    /// <param name="exePath">Absolute path of the executable to spawn for every command.</param>
    /// <param name="rewriteCommand">
    /// Optional transform applied to the raw command line the agent sent before
    /// it's passed to the process. Typical use: strip a redundant "azv2 " prefix
    /// because the exe is already azv2. Defaults to identity.
    /// </param>
    public ProcessExecutor(string exePath, Func<string, string>? rewriteCommand = null)
    {
        _exePath = exePath ?? throw new ArgumentNullException(nameof(exePath));
        _rewriteCommand = rewriteCommand ?? (s => s);
    }

    /// <summary>
    /// Implements <see cref="RunCommandTool.RunCommandExecutor"/>. Spawns the
    /// configured exe with the rewritten command line, tees stdout / stderr
    /// to <see cref="Console.Out"/> live and folds them into the outcome
    /// returned to the LLM.
    /// </summary>
    public async Task<ExecutionOutcome> RunAsync(string commandLine, CancellationToken ct)
    {
        var rewritten = _rewriteCommand(commandLine);

        var stdoutBuf = new StringBuilder();
        var stderrBuf = new StringBuilder();

        var psi = new ProcessStartInfo
        {
            FileName = _exePath,
            Arguments = rewritten,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // Critical: do NOT inherit stdin. If the child reads keystrokes it
            // can race with the parent's approval prompt.
            RedirectStandardInput = true,
            CreateNoWindow = true,
        };

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.Start();
        proc.StandardInput.Close();

        var stdoutTask = StreamAsync(proc.StandardOutput, stdoutBuf, ConsoleColor.Gray);
        var stderrTask = StreamAsync(proc.StandardError, stderrBuf, ConsoleColor.Red);

        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);

        return proc.ExitCode == 0
            ? new ExecutionOutcome(true, commandLine, 0, Stdout: stdoutBuf.ToString())
            : new ExecutionOutcome(false, commandLine, proc.ExitCode,
                ErrorMessage: stderrBuf.ToString().TrimEnd(),
                Stdout: stdoutBuf.ToString());
    }

    private static async Task StreamAsync(StreamReader reader, StringBuilder buf, ConsoleColor color)
    {
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            buf.AppendLine(line);
            var prev = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = color;
                Console.WriteLine(line);
            }
            finally
            {
                Console.ForegroundColor = prev;
            }
        }
    }
}
