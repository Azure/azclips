// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace Core.Configuration.Options;

public sealed class AssemblyLoadingOptions
{
    public string BuiltinInstallationDir { get; set; } = GetDefaultBuiltinInstallationDir();

    public string VersionedInstallationDir { get; set; } = GetDefaultVersionedInstallationDir();

    public string ExtensionFolderPrefix { get; set; } = GetDefaultExtensionFolderPrefix();

    /// <summary>
    /// When true, <see cref="Core.Cli.MultiVersionExtensionLoader"/> skips its process-wide ALC cache
    /// and constructs a fresh <see cref="System.Runtime.Loader.AssemblyLoadContext"/> on every load.
    /// Test/benchmark hook; set via the <c>--no-cache</c> CLI flag or <c>CIRRUS_NO_EXTENSION_CACHE</c>
    /// environment variable.
    /// </summary>
    public bool BypassExtensionCache { get; set; }

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
        // Always use ~/.cirrus/extensions regardless of build configuration.
        // Installed extensions live there, even during local development.
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".cirrus", "extensions");
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
