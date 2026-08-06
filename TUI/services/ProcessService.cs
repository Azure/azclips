using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Core.Tui.Services;

/// <summary>
/// Result reported when a process completes.
/// </summary>
record ProcessResult(int ExitCode, List<string> StderrLines);

/// <summary>
/// Manages CLI and local shell process execution with streaming output.
/// </summary>
class ProcessService
{
    private static readonly string CliPath = FindCliPath();

    private Process? _runningProcess;
    private readonly Stopwatch _stopwatch = new();

    public bool IsRunning { get; private set; }
    public int ElapsedSeconds => _stopwatch.IsRunning ? (int)_stopwatch.Elapsed.TotalSeconds : 0;

    /// <summary>
    /// Run a command via the az CLI, streaming output to the callback.
    /// </summary>
    public async Task RunCliAsync(string args, Action<HistoryItemType, string> onLine, Action<ProcessResult> onDone)
    {
        IsRunning = true;
        _stopwatch.Restart();

        try
        {
            _runningProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = CliPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true,
            };

            _runningProcess.Start();

            var stdoutTask = StreamLinesAsync(_runningProcess.StandardOutput, HistoryItemType.Output, onLine);
            var stderrTask = StreamLinesAsync(_runningProcess.StandardError, HistoryItemType.Error, onLine);

            await Task.WhenAll(stdoutTask, stderrTask);
            await _runningProcess.WaitForExitAsync();

            onDone(new ProcessResult(_runningProcess.ExitCode, []));
        }
        catch (Exception ex)
        {
            onLine(HistoryItemType.Error, ex.Message);
            onDone(new ProcessResult(-1, []));
        }
        finally
        {
            Cleanup();
        }
    }

    /// <summary>
    /// Run a shell command in the given working directory, streaming output.
    /// stderr is rendered as Output (not Error) during execution;
    /// on non-zero exit, stderr lines are re-reported as errors via onDone.
    /// </summary>
    public async Task RunLocalAsync(string command, string workingDirectory,
        Action<HistoryItemType, string> onLine, Action<ProcessResult> onDone)
    {
        IsRunning = true;
        _stopwatch.Restart();

        try
        {
            var isWindows = OperatingSystem.IsWindows();
            var startInfo = new ProcessStartInfo
            {
                FileName = isWindows ? "cmd.exe" : "/bin/sh",
                Arguments = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            // Add az directory to PATH so "az" is resolvable as a command
            var cliDir = Path.GetDirectoryName(CliPath);
            if (!string.IsNullOrEmpty(cliDir))
            {
                var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                startInfo.Environment["PATH"] = cliDir + Path.PathSeparator + currentPath;
            }

            // Force color output even though stdout is redirected (not a TTY)
            startInfo.Environment["TERM"] = "xterm-256color";
            startInfo.Environment["FORCE_COLOR"] = "1";       // Node.js / chalk
            startInfo.Environment["CLICOLOR_FORCE"] = "1";    // BSD / macOS convention

            // Git ignores FORCE_COLOR; use its env-based config override
            var gitConfigCount = int.TryParse(
                Environment.GetEnvironmentVariable("GIT_CONFIG_COUNT"), out var c) ? c : 0;
            startInfo.Environment["GIT_CONFIG_COUNT"] = (gitConfigCount + 1).ToString();
            startInfo.Environment[$"GIT_CONFIG_KEY_{gitConfigCount}"] = "color.ui";
            startInfo.Environment[$"GIT_CONFIG_VALUE_{gitConfigCount}"] = "always";

            _runningProcess = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true,
            };

            _runningProcess.Start();

            // Stream stderr as Output — many tools (git, npm) write info to stderr
            var stderrLines = new List<string>();
            var stdoutTask = StreamLinesAsync(_runningProcess.StandardOutput, HistoryItemType.Output, onLine);
            var stderrTask = StreamLinesAsync(_runningProcess.StandardError, HistoryItemType.Output, onLine, stderrLines);

            await Task.WhenAll(stdoutTask, stderrTask);
            await _runningProcess.WaitForExitAsync();

            onDone(new ProcessResult(_runningProcess.ExitCode, stderrLines));
        }
        catch (Exception ex)
        {
            onLine(HistoryItemType.Error, ex.Message);
            onDone(new ProcessResult(-1, []));
        }
        finally
        {
            Cleanup();
        }
    }

    /// <summary>
    /// Kill the running process immediately.
    /// </summary>
    public void Cancel()
    {
        try { _runningProcess?.Kill(entireProcessTree: true); }
        catch { /* already exited */ }
    }

    private void Cleanup()
    {
        _runningProcess?.Dispose();
        _runningProcess = null;
        _stopwatch.Stop();
        IsRunning = false;
    }

    private static async Task StreamLinesAsync(
        StreamReader reader, HistoryItemType type,
        Action<HistoryItemType, string> onLine,
        List<string>? capture = null)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            var trimmed = line.TrimEnd('\r');
            if (type == HistoryItemType.Error && string.IsNullOrWhiteSpace(trimmed)) continue;

            var classified = ClassifyLine(trimmed, type);
            if (classified is null) continue;

            var (actualType, text) = classified.Value;
            capture?.Add(text);
            onLine(actualType, text);
        }
    }

    internal static (HistoryItemType type, string text)? ClassifyLine(string line, HistoryItemType defaultType)
    {
        // Strip ANSI escape codes once and reuse the result for both prefix matching
        // (via TrimStart on `clean`) and the returned text. Avoids running Regex.Replace
        // twice per line since this is called for every stderr line.
        var stripped = StripAnsi(line);
        var clean = stripped.TrimStart();

        // The AzClips CLI emits diagnostics on stderr with the canonical "LEVEL: message"
        // prefix (see decision registry). Classify by prefix so warnings/info don't get
        // stamped as errors under RunCliAsync (which uses defaultType=Error).
        // Case-sensitive (Ordinal) match: AzClips always emits uppercase, but tools like
        // git/npm emit lowercase ("warning:"), so we intentionally leave those alone —
        // RunLocalAsync callers still see git output as HistoryItemType.Output.

        // Drop verbose/debug lines entirely — too noisy for the TUI history.
        if (clean.StartsWith("VERBOSE:", StringComparison.Ordinal) ||
            clean.StartsWith("DEBUG:",   StringComparison.Ordinal))
        {
            return null;
        }

        if (clean.StartsWith("ERROR:", StringComparison.Ordinal))
        {
            return (HistoryItemType.Error, stripped);
        }

        if (clean.StartsWith("WARNING:", StringComparison.Ordinal))
        {
            return (HistoryItemType.Warn, stripped);
        }

        if (clean.StartsWith("INFO:", StringComparison.Ordinal))
        {
            return (HistoryItemType.Info, stripped);
        }

        return (defaultType, stripped);
    }

    private static string StripAnsi(string text)
        => Regex.Replace(text, @"\x1b\[[0-9;]*m", "");

    private static string FindCliPath()
    {
        var exeName = OperatingSystem.IsWindows() ? "az.exe" : "az";
        var baseDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "artifacts", "Debug", "Core.Cli", exeName);
            if (File.Exists(candidate)) return candidate;

            candidate = Path.Combine(dir.FullName, "bin", exeName);
            if (File.Exists(candidate)) return candidate;

            dir = dir.Parent;
        }

        return "az"; // fallback — hope it's on PATH
    }
}
