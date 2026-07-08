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

        // Packaged layout: copilot lives inside the Agent extension's
        // deps tree under a NuGet-style path so all agent-only
        // assets stay under Extensions/Microsoft.Azure.Azclips.Extensions.Agent/.
        //   <baseDir>/Extensions/Microsoft.Azure.Azclips.Extensions.Agent/
        //     deps/github.copilot.sdk/<version>/runtimes/<rid>/native/copilot[.exe]
        // Version is discovered at runtime — there is only one; SDK upgrades
        // create a new version dir and we pick whichever is present.
        var sdkRoot = Path.Combine(AppContext.BaseDirectory,
            "Extensions", "Microsoft.Azure.Azclips.Extensions.Agent",
            "deps", "github.copilot.sdk");
        if (Directory.Exists(sdkRoot))
        {
            foreach (var versionDir in Directory.EnumerateDirectories(sdkRoot))
            {
                var packaged = Path.Combine(versionDir, "runtimes", rid, "native", binaryName);
                if (File.Exists(packaged)) return packaged;
            }
        }

        // Dev-loop layout: copilot.exe is copied to the agent's own build dir
        // as part of NuGet restore, matching the traditional
        //   <baseDir>/runtimes/<rid>/native/copilot[.exe]
        // location. Kept so `dotnet build` + `dotnet run` still finds it
        // without packaging.
        var devPath = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", binaryName);
        return File.Exists(devPath) ? devPath : null;
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
