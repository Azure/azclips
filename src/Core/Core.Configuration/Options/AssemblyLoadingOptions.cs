// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Configuration.Options;

public sealed class AssemblyLoadingOptions
{
    public string InstalledExtensionsRoot { get; set; } = GetDefaultInstalledExtensionsRoot();

    public string ExtensionFolderPrefix { get; set; } = GetDefaultExtensionFolderPrefix();

    /// <summary>
    /// When true, <see cref="Core.Cli.InstalledExtensionLoader"/> skips its process-wide ALC cache
    /// and constructs a fresh <see cref="System.Runtime.Loader.AssemblyLoadContext"/> on every load.
    /// Test/benchmark hook; set via the <c>--no-cache</c> CLI flag or
    /// <c>AZCLIPS_NO_EXTENSION_CACHE</c> environment variable.
    /// </summary>
    public bool BypassExtensionCache { get; set; }

    private static string GetDefaultInstalledExtensionsRoot()
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
