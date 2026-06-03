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
                "Bundled Copilot CLI not found under runtimes/<rid>/native — " +
                "NuGet restore likely incomplete.");

    private static string? FindBundledExe()
    {
        var binaryName = OperatingSystem.IsWindows() ? "copilot.exe" : "copilot";
        var rid = GetPortableRid()
            ?? Path.GetFileName(RuntimeInformation.RuntimeIdentifier);
        var path = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", binaryName);
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
