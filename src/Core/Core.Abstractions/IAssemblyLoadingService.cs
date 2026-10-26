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

    /// <summary>Loads only the selected assembly through the client provider.</summary>
    /// <remarks>
    /// Services without reference support reject the request rather than searching by name.
    /// </remarks>
    Assembly LoadExtension(AssemblyReference reference)
        => throw new NotSupportedException("This service does not support loading by assembly reference.");

    /// <summary>
    /// Returns all on-disk extension installations paired with a lazy assembly loader for
    /// each. Delegates to <see cref="IAssemblyLoadingProvider.GetAllInstalledExtensions"/>.
    /// Returns <c>null</c> when no extensions are discovered.
    /// Entries with assembly references bind their deferred load through the client provider;
    /// entries without references retain name/version loading.
    /// </summary>
    IList<(InstalledExtensionRef Info, Func<Assembly> Load)>? LoadAllExtensions();
}