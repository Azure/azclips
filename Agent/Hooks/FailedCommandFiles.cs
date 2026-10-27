using System.Text;
using GitHub.Copilot;

namespace Core.Agent.Hooks;

/// <summary>
/// The files that the failed command names, such as the template in
/// <c>deployment group create --template-file web-app.bicep</c>. Reading one needs no
/// approval: the user pointed the command at it, and the cause of the failure is often
/// inside it. Any other read still asks the user, so one-shot mode, where nobody can
/// answer, still denies it.
/// </summary>
internal sealed class FailedCommandFiles
{
    // The CLI starts the agent in its install directory and forwards the directory that the
    // user ran az from. The name must match AgentLauncher.CallerCwdEnvVar, as in EnvironmentProber.
    internal const string CallerDirectoryVariable = "AZCLIPS_CALLER_CWD";

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly HashSet<string> _paths;

    private FailedCommandFiles(HashSet<string> paths) => _paths = paths;

    // Resolves relative paths from the directory that the CLI forwarded, or from this
    // process's directory when the agent was started directly.
    internal static FailedCommandFiles ForCaller(string? failedCommand)
    {
        var callerDirectory = Environment.GetEnvironmentVariable(CallerDirectoryVariable);
        return From(failedCommand,
            string.IsNullOrEmpty(callerDirectory) ? Directory.GetCurrentDirectory() : callerDirectory);
    }

    // Relative paths resolve against workingDirectory, where the failed command ran.
    internal static FailedCommandFiles From(string? failedCommand, string workingDirectory)
    {
        var paths = new HashSet<string>(PathComparer);
        var command = failedCommand ?? string.Empty;
        // JoinShellQuoted writes a quote inside an argument as \" but keeps a backslash before the
        // closing quote as it is ("C:\My Files\"), so a command with \" cannot be split back
        // reliably. Such a command names no files.
        if (command.Contains("\\\"", StringComparison.Ordinal))
            return new FailedCommandFiles(paths);
        foreach (var argument in SplitArguments(command))
        {
            // An option can carry its value after "=" (--template-file=main.bicep), and a
            // value can name a file after "@" (--parameters @params.json).
            var value = argument.StartsWith('-') ? argument[(argument.IndexOf('=') + 1)..] : argument;
            if (value.StartsWith('@'))
                value = value[1..];
            if (TryGetFullPath(value, workingDirectory, out var path) && File.Exists(path))
                paths.Add(path);
        }
        return new FailedCommandFiles(paths);
    }

    // True for a plain read of one of these files by absolute path. The read tool would take
    // a relative path from the agent's own directory, and a sandbox bypass still asks.
    internal bool Allows(PermissionRequest request) =>
        request is PermissionRequestRead { RequestSandboxBypass: not true, Path: { } path }
        && Path.IsPathFullyQualified(path)
        && TryGetFullPath(path, Path.GetPathRoot(path)!, out var fullPath)
        && _paths.Contains(fullPath);

    // Splits a command that AgentLauncher.JoinShellQuoted built: arguments are joined with spaces, and an
    // argument with a space, a tab or a quote is wrapped in quotes. Any other character, such as a
    // line break, belongs to its argument. An argument with an unterminated quote is dropped, so no
    // fragment of it can name a file.
    private static List<string> SplitArguments(string command)
    {
        var arguments = new List<string>();
        var current = new StringBuilder();
        var inArgument = false;
        var quoted = false;
        foreach (var c in command)
        {
            if (quoted)
            {
                if (c == '"')
                    quoted = false;
                else
                    current.Append(c);
            }
            else if (c == '"')
            {
                quoted = true;
                inArgument = true;
            }
            else if (c is ' ' or '\t')
            {
                if (inArgument)
                    arguments.Add(current.ToString());
                current.Clear();
                inArgument = false;
            }
            else
            {
                current.Append(c);
                inArgument = true;
            }
        }
        if (inArgument && !quoted)
            arguments.Add(current.ToString());
        return arguments;
    }

    private static bool TryGetFullPath(string? value, string workingDirectory, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith('-'))
            return false;
        try
        {
            path = Path.GetFullPath(value, workingDirectory);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
