using System.Runtime.InteropServices;

namespace Core.Agent;

/// <summary>
/// Resolves the bundled Copilot CLI executable from the SDK NuGet package.
/// The SDK kills the daemon by PID (<c>Process.Kill(entireProcessTree: true)</c>),
/// so renaming the shipped binary for Task Manager attribution isn't worth the
/// write — MSIX install dirs are read-only and the copy fails there.
/// </summary>
internal static class BundledDaemonResolver
{
    public static string Resolve() =>
        FindBundledExe()
            ?? throw new InvalidOperationException(
                "Bundled Copilot CLI not found. " +
                (AgentInstallLayout.IsPackaged
                    ? "Packaging bug — stage_agent should have staged the native binary under Extensions/Microsoft.Azure.Azclips.Extensions.Agent/deps/runtimes/<rid>/native/."
                    : "Dev build bug — NuGet restore should place copilot under runtimes/<rid>/native/."));

    private static string? FindBundledExe()
    {
        var binaryName = OperatingSystem.IsWindows() ? "copilot.exe" : "copilot";
        // The Copilot SDK stages native assets under portable OS/architecture
        // RIDs even when CopilotCliBinaryPath points to a musl executable.
        var rid = GetPortableRid()
            ?? Path.GetFileName(RuntimeInformation.RuntimeIdentifier);

        // Path derived from AgentInstallLayout (packaged reads deps.json for the
        // referenced SDK version — a rolled-back build stays on its own version,
        // not whatever is lexicographically newest). Dev uses NuGet's default
        // runtimes/<rid>/native/ next to the exe.
        var path = Path.Combine(AgentInstallLayout.SdkNativeDir(rid), binaryName);
        return File.Exists(path) ? path : null;
    }

    private static string? GetPortableRid()
    {
        string os;
        if (OperatingSystem.IsWindows()) os = "win";
        else if (OperatingSystem.IsLinux()) os = "linux";
        else if (OperatingSystem.IsMacOS()) os = "osx";
        else return null;

        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null,
        };

        return arch is null ? null : $"{os}-{arch}";
    }
}
