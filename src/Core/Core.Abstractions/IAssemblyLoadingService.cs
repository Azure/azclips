// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;

namespace Core.Abstractions;

/// <summary>
/// Service for loading extension assemblies and resolving extension indices.
/// Abstracts the assembly loading mechanism so engines don't need to manage
/// extension loaders, directory resolution, or AssemblyLoadContext directly.
/// </summary>
public interface IAssemblyLoadingService
{
    /// <summary>
    /// Loads an extension assembly by name, optionally targeting a specific version.
    /// Throws an exception if the extension cannot be found by any loader.
    /// </summary>
    /// <param name="extensionName">The logical extension name (e.g., "Resources", "Login").</param>
    /// <param name="extensionVersion">Optional version string; null uses the provider's legacy resolution policy.</param>
    /// <returns>The loaded assembly.</returns>
    Assembly LoadExtension(string extensionName, string? extensionVersion = null);

    /// <summary>Loads only the selected installation; failure never retries another copy.</summary>
    /// <remarks>
    /// Prefer this overload when routing supplied an installation handle. The default
    /// implementation invokes the handle directly, so legacy providers need not implement
    /// a second search operation.
    /// </remarks>
    Assembly LoadExtension(ExtensionInstallation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        return installation.Load();
    }

    /// <summary>
    /// Returns all on-disk extension installations paired with a lazy assembly loader for
    /// each. Delegates to <see cref="IAssemblyLoadingProvider.GetAllInstalledExtensions"/>.
    /// Returns <c>null</c> when no extensions are discovered.
    /// Handle-bearing entries bind their deferred load to that exact installation;
    /// entries without handles retain legacy name/version loading.
    /// </summary>
    IList<(InstalledExtensionRef Info, Func<Assembly> Load)>? LoadAllExtensions();
}