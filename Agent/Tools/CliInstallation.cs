// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Core.Agent.Tools;

internal static class CliInstallation
{
    public static string ResolvePath(string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        if (Path.IsPathRooted(executable) || executable.Contains(Path.DirectorySeparatorChar) ||
            executable.Contains(Path.AltDirectorySeparatorChar))
        {
            return Path.GetFullPath(executable);
        }

        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            var candidate = Path.Combine(directory.Trim('"'), executable);
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            if (OperatingSystem.IsWindows() && File.Exists(candidate + ".exe"))
                return Path.GetFullPath(candidate + ".exe");
        }

        throw new FileNotFoundException("The Azclips executable could not be located.");
    }

    public static string GetVersion(string executable)
    {
        var path = ResolvePath(executable);
        var managedPath = Path.ChangeExtension(path, ".dll");
        var versionPath = File.Exists(managedPath) ? managedPath : path;
        return FileVersionInfo.GetVersionInfo(versionPath).ProductVersion ?? "unknown";
    }
}
