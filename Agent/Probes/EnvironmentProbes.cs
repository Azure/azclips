namespace Core.Agent.Probes;

internal static class EnvironmentProbes
{
    public static bool IsSshSession() =>
        !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("SSH_CLIENT")) ||
        !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("SSH_CONNECTION")) ||
        !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("SSH_TTY"));

    public static bool IsBrowserAvailable(bool sshSession)
    {
        if (sshSession) return false;

        if (OperatingSystem.IsWindows())
        {
            var session = System.Environment.GetEnvironmentVariable("SESSIONNAME");
            if (string.IsNullOrEmpty(session)) return false;
            return !session.StartsWith("Services", StringComparison.OrdinalIgnoreCase);
        }

        if (OperatingSystem.IsMacOS()) return true;

        return !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("DISPLAY")) ||
               !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
    }

    public static string DetectShell()
    {
        var shell = System.Environment.GetEnvironmentVariable("SHELL");
        if (!string.IsNullOrEmpty(shell)) return Path.GetFileName(shell);

        if (OperatingSystem.IsWindows())
        {
            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("POWERSHELL_DISTRIBUTION_CHANNEL")))
                return "pwsh";
            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("PSModulePath")))
                return "pwsh-or-cmd";
            var comspec = System.Environment.GetEnvironmentVariable("ComSpec");
            return !string.IsNullOrEmpty(comspec) ? Path.GetFileName(comspec) : "cmd";
        }

        return "unknown";
    }
}
