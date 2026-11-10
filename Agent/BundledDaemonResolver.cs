using System.Runtime.InteropServices;

namespace Core.Agent;

/// <summary>
/// Resolves the bundled Copilot CLI executable from the SDK NuGet package.
/// The SDK kills the daemon by PID (<c>Process.Kill(entireProcessTree: true)</c>),
/// so renaming the shipped binary for Task Manager attribution isn't worth the
/// write — install directories may be read-only and the copy would fail there.
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
        var rid = Path.GetFileName(RuntimeInformation.RuntimeIdentifier);

        // Path derived from AgentInstallLayout (packaged reads deps.json for the
        // referenced SDK version — a rolled-back build stays on its own version,
        // not whatever is lexicographically newest). Dev uses NuGet's default
        // runtimes/<rid>/native/ next to the exe.
        var path = Path.Combine(AgentInstallLayout.SdkNativeDir(rid), binaryName);
        return File.Exists(path) ? path : null;
    }
}
