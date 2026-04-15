using System.Runtime.InteropServices;

namespace Core.Configuration.Options;

public sealed class AssemblyLoadingOptions
{
    public string BuiltinInstallationDir { get; set; } = GetDefaultBuiltinInstallationDir();

    public string VersionedInstallationDir { get; set; } = GetDefaultVersionedInstallationDir();

    public string ExtensionFolderPrefix { get; set; } = GetDefaultExtensionFolderPrefix();

    private static string GetDefaultBuiltinInstallationDir()
    {
#if DEBUG
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
#else
        return Path.Combine(AppContext.BaseDirectory, "Extensions");
#endif
    }

    private static string GetDefaultVersionedInstallationDir()
    {
#if DEBUG
        return GetDefaultBuiltinInstallationDir();
#else
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".cirrus", "extensions");
#endif
    }

    private static string GetDefaultExtensionFolderPrefix()
    {
#if DEBUG
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Microsoft.Azure.Cirrus.Extensions."
            : "Microsoft.Azure.Cirrus.Extensions.";
#else
        return string.Empty;
#endif
    }
}
