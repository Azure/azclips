// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Benchmark.Startup;

/// <summary>
/// Best-effort OS page-cache eviction for cold-start measurement.
/// Linux (CI): sync + drop_caches via passwordless sudo. Windows (dev): EmptyStandbyList.exe.
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

    private static bool TryEvict(bool probe)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (!File.Exists("/proc/sys/vm/drop_caches")) return false;
                if (probe) return true;
                return RunBash("sync; echo 3 | sudo tee /proc/sys/vm/drop_caches > /dev/null");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var tool = FindTool("EmptyStandbyList.exe");
                if (tool is null) return false;
                if (probe) return true;
                return Run(tool, "standbylist");
            }

            return false; // macOS / other — not part of the supported cold matrix
        }
        catch
        {
            return false;
        }
    }

    private static string? FindTool(string fileName)
    {
        // 1) next to the benchmark binary (repo tooling), 2) PATH
        var local = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(local)) return local;

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), fileName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
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
        _ = p.StandardOutput.ReadToEnd();
        _ = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return p.ExitCode == 0;
    }
}
