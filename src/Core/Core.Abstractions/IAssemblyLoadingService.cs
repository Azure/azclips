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
    /// <summary>Returns the current client product version.</summary>
    string GetProductVersion();

    /// <summary>
    /// Loads an extension assembly by name, optionally targeting a specific version.
    /// Throws an exception if the extension cannot be found by any loader.
    /// </summary>
    /// <param name="extensionName">The logical extension name (e.g., "Resources", "Login").</param>
    /// <param name="extensionVersion">Optional version string. If null, the latest available version is loaded.</param>
    /// <returns>The loaded assembly.</returns>
    Assembly LoadExtension(string extensionName, string? extensionVersion = null);

    /// <summary>
    /// Returns all on-disk extension installations paired with a lazy assembly loader for
    /// each. Delegates to <see cref="IAssemblyLoadingProvider.GetAllInstalledExtensions"/>.
    /// Returns <c>null</c> when no extensions are discovered.
    /// </summary>
    IList<(InstalledExtensionRef Info, Func<Assembly> Load)>? LoadAllExtensions();
}