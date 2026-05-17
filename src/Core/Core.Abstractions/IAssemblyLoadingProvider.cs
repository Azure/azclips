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

    IList<Tuple<string, string?>>? GetAllInstalledExtensions();
}