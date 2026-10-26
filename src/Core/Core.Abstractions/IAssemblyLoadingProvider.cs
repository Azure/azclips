// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

using System.Reflection;

public interface IAssemblyLoadingProvider
{
    /// <summary>
    /// Loads an extension assembly by its name and optional version.
    /// </summary>
    /// <param name="extensionName">The name of the extension to load.</param>
    /// <param name="extensionVersion">The optional version of the extension to load.</param>
    /// <returns>The loaded assembly.</returns>
    Assembly LoadExtension(string extensionName, string? extensionVersion = null);

    /// <summary>
    /// Loads the exact assembly identified by a reference issued by this client.
    /// Providers without reference support reject the request rather than searching by name.
    /// </summary>
    Assembly LoadExtension(AssemblyReference reference)
        => throw new NotSupportedException("This provider does not support loading by assembly reference.");

    /// <summary>
    /// Enumerates all extension installations discovered on disk. Each entry carries
    /// the folder name (which is passed verbatim back to <see cref="LoadExtension"/>) and
    /// the resolved version. Providers supporting exact loading attach
    /// <see cref="InstalledExtensionRef.AssemblyReference"/> without loading the assembly.
    /// May return <c>null</c> when the provider has no install root
    /// (e.g. in-memory or test providers).
    /// </summary>
    IList<InstalledExtensionRef>? GetAllInstalledExtensions();
}