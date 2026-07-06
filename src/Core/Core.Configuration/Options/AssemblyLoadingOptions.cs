// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Configuration.Options;

public sealed class AssemblyLoadingOptions
{
    public string BuiltinInstallationDir { get; set; } = GetDefaultBuiltinInstallationDir();

    public string VersionedInstallationDir { get; set; } = GetDefaultVersionedInstallationDir();

    public string ExtensionFolderPrefix { get; set; } = GetDefaultExtensionFolderPrefix();

    /// <summary>
    /// When true, <see cref="Core.Cli.MultiVersionExtensionLoader"/> skips its process-wide ALC cache
    /// and constructs a fresh <see cref="System.Runtime.Loader.AssemblyLoadContext"/> on every load.
    /// Test/benchmark hook; set via the <c>--no-cache</c> CLI flag or
    /// <c>AZCLIPS_NO_EXTENSION_CACHE</c> environment variable.
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
        // Always use ~/.azclips/extensions regardless of build configuration.
        // Installed extensions live there, even during local development.
        return HomePaths.GetDefaultExtensionsRoot();
    }

    private static string GetDefaultExtensionFolderPrefix()
    {
#if DEBUG
        return "Microsoft.Azure.Azclips.Extensions.";
#else
        return string.Empty;
#endif
    }
}
