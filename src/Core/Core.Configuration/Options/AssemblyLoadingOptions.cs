using System.Runtime.InteropServices;

namespace Core.Configuration.Options;

public sealed class AssemblyLoadingOptions
{
    public string BuiltinInstallationDir { get; set; } = GetDefaultBuiltinInstallationDir();

    public string VersionedInstallationDir { get; set; } = GetDefaultVersionedInstallationDir();

    public string ExtensionFolderPrefix { get; set; } = GetDefaultExtensionFolderPrefix();

    private static string GetDefaultBuiltinInstallationDir()
    {
        return Path.Combine(AppContext.BaseDirectory, "Extensions");
    }

    private static string GetDefaultVersionedInstallationDir()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".cirrus", "extensions");
    }

    private static string GetDefaultExtensionFolderPrefix()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Microsoft.Azure.Cirrus.Extensions."
            : "Microsoft.Azure.Cirrus.Extensions.";
    }
}
