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
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly HashSet<string> _paths;
    private readonly string _workingDirectory;

    private FailedCommandFiles(HashSet<string> paths, string workingDirectory)
    {
        _paths = paths;
        _workingDirectory = workingDirectory;
    }

    // Relative paths resolve against workingDirectory, where the failed command ran.
    internal static FailedCommandFiles From(string? failedCommand, string workingDirectory)
    {
        var paths = new HashSet<string>(PathComparer);
        foreach (var token in (failedCommand ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            // A file can also follow "=" (--template-file=main.bicep) or "@" (--parameters @params.json).
            var value = token[(token.IndexOf('=') + 1)..].Trim('"', '\'').TrimStart('@');
            if (TryGetFullPath(value, workingDirectory, out var path) && File.Exists(path))
                paths.Add(path);
        }
        return new FailedCommandFiles(paths, workingDirectory);
    }

    // True for a plain read of one of these files. A request to read outside the sandbox still asks.
    internal bool Allows(PermissionRequest request) =>
        request is PermissionRequestRead { RequestSandboxBypass: not true } read
        && TryGetFullPath(read.Path, _workingDirectory, out var path)
        && _paths.Contains(path);

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
