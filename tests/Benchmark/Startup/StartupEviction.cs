// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Benchmark.Startup;

/// <summary>
/// Best-effort OS page-cache eviction for cold-start measurement.
/// Linux (CI): sync + drop_caches via passwordless sudo. Windows (dev): an in-process standby-list
/// purge (see <see cref="WindowsPageCacheEvictor"/>) — no external tool required.
/// When eviction is not available the cold benchmarks are skipped by Program.cs (never reported
/// as cold-but-actually-warm).
/// </summary>
public static class StartupEviction
{
    /// <summary>True if this machine can evict the OS page cache for cold runs.</summary>
    public static bool IsAvailable() => TryEvict(probe: true);

    /// <summary>Evict the OS page cache. Logs and continues if eviction is unavailable.</summary>
    public static void Evict()
    {
        if (!TryEvict(probe: false))
        {
            Console.WriteLine("[Startup] cold eviction unavailable at runtime — launch was NOT cold");
        }
    }

    /// <summary>
    /// Human-readable reason cold eviction is unavailable on this machine, used to give the operator
    /// an actionable hint (e.g. "run elevated") instead of a bare skip message.
    /// </summary>
    public static string DescribeUnavailable()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return IsWindowsAdministrator()
                ? "Windows standby-list purge failed unexpectedly"
                : "Windows cold runs require an elevated (Administrator) terminal";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Linux cold runs require /proc/sys/vm/drop_caches and passwordless sudo";
        }

        return "cold eviction is only supported on Windows and Linux";
    }

    private static bool TryEvict(bool probe)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (!File.Exists("/proc/sys/vm/drop_caches")) return false;
                // Capability check: passwordless sudo. `-n` never prompts and exits non-zero
                // if a password would be required. No system mutation, so safe on the probe path.
                if (!Run("/bin/bash", "-c \"sudo -n true\"")) return false;
                if (probe) return true;
                return RunBash("sync; echo 3 | sudo tee /proc/sys/vm/drop_caches > /dev/null");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Purging the standby list requires SeProfileSingleProcessPrivilege, which is only
                // grantable to an elevated process. CanPurge() verifies that without mutating the
                // cache, so it doubles as the probe-path capability check.
                if (!WindowsPageCacheEvictor.CanPurge()) return false;
                if (probe) return true;
                return WindowsPageCacheEvictor.PurgeStandbyList();
            }

            return false; // macOS / other — not part of the supported cold matrix
        }
        catch
        {
            return false;
        }
    }

    private static bool IsWindowsAdministrator()
    {
        if (!OperatingSystem.IsWindows()) return false;
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    private static bool RunBash(string script) => Run("/bin/bash", $"-c \"{script}\"");

    private static bool Run(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi);
        if (p is null) return false;
        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();
        p.WaitForExit();
        Task.WaitAll(stdoutTask, stderrTask);
        return p.ExitCode == 0;
    }
}
