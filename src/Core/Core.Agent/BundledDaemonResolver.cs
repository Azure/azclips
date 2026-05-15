using System.Runtime.InteropServices;

namespace Core.Agent;

/// <summary>
/// Resolves the bundled Copilot CLI executable from the SDK NuGet package and
/// hands back a path with a cirrus-branded filename. We don't rename the
/// shipped binary (that would fight NuGet's content hash on every restore);
/// we copy-on-write a sibling named "cirrus-agent.exe" so Task Manager /
/// tasklist clearly attributes the daemon to cirrus instead of a generic
/// "copilot.exe" that could belong to any consumer of the SDK on the box.
/// </summary>
internal static class BundledDaemonResolver
{
    public static string Resolve()
    {
        var bundled = FindBundledExe()
            ?? throw new InvalidOperationException(
                "Bundled Copilot CLI not found under runtimes/<rid>/native — " +
                "NuGet restore likely incomplete.");

        var brandedName = OperatingSystem.IsWindows() ? "cirrus-agent.exe" : "cirrus-agent";
        var branded = Path.Combine(Path.GetDirectoryName(bundled)!, brandedName);

        if (!File.Exists(branded) || File.GetLastWriteTimeUtc(bundled) > File.GetLastWriteTimeUtc(branded))
        {
            File.Copy(bundled, branded, overwrite: true);
        }

        return branded;
    }

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
