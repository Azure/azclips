// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli;

using System.Reflection;
using Core.Abstractions;

public class CLIAssemblyLoadingProvider : IAssemblyLoadingProvider
{
    private readonly IReadOnlyList<IExtensionLoader> _extensionLoaders;
    private readonly ILoggingService _loggingService;
    private readonly SelectedExtensionLocations? _selectedLocations;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IExtensionLoader> _loaderByLocation =
        new(StringComparer.OrdinalIgnoreCase);

    public CLIAssemblyLoadingProvider(
        IEnumerable<IExtensionLoader> extensionLoaders,
        ILoggingService loggingService,
        SelectedExtensionLocations? selectedLocations = null)
    {
        // Enumeration and search order is by tier (Bundled, then Installed), not registration
        // order. Equal-version candidates are selected in this order, so it decides which wins.
        _extensionLoaders = extensionLoaders.OrderBy(static loader => loader.Tier).ToList();
        _loggingService = loggingService;
        _selectedLocations = selectedLocations;
    }

    private Assembly? LoadExtensionFromLoaders(string extensionName, string? version = null) => _extensionLoaders
                    .Select(loader => loader.LoadExtension(extensionName, version))
                    .FirstOrDefault(assembly => assembly != null);

    public Assembly LoadExtension(string extensionName, string? extensionVersion = null)
    {
        _loggingService.Info($"Processing command with extension '{extensionName}'.");

        if (_selectedLocations?.TryGet(extensionName, extensionVersion, out var location) == true)
        {
            return LoadSelected(extensionName, extensionVersion, location);
        }

        return LoadExtensionFromLoaders(extensionName, extensionVersion) ?? throw new ExtensionLoadException($"Extension '{extensionName}' could not be found by any extension loader.");
    }

    /// <summary>
    /// Loads the DLL at a location this provider reported from <see cref="GetAllInstalledExtensions"/>,
    /// through the loader that reported it. Never re-searches by name and version.
    /// </summary>
    public Assembly LoadExtensionAt(string extensionName, string? extensionVersion, string location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        return LoadSelected(extensionName, extensionVersion, location);
    }

    /// <summary>
    /// Loads the DLL routing selected, through the loader that reported it. Fails rather than
    /// re-searching, because a search could load a different copy than the one routed to.
    /// </summary>
    private Assembly LoadSelected(string extensionName, string? extensionVersion, string location)
    {
        _loggingService.Debug($"[{nameof(CLIAssemblyLoadingProvider)}] Loading '{extensionName}' (version='{extensionVersion ?? "any"}') from '{location}'.");
        if (!_loaderByLocation.TryGetValue(location, out var loader))
        {
            throw new InvalidOperationException($"Extension '{extensionName}' is at '{location}', but no extension loader reported that location.");
        }

        return loader.LoadExtensionFromPath(extensionName, extensionVersion, location)
            ?? throw new InvalidOperationException($"Extension '{extensionName}' could not be loaded from '{location}'. It may have been removed; run the command again.");
    }

    /// <summary>
    /// Reports every copy each loader finds, in tier order. Selection (pins, highest version,
    /// tier) is left to <c>ExtensionCandidateSelector</c>; this method doesn't choose between copies.
    /// </summary>
    public IList<InstalledExtensionRef>? GetAllInstalledExtensions()
    {
        var result = new List<InstalledExtensionRef>();

        foreach (var loader in _extensionLoaders)
        {
            foreach (var (name, version, location) in loader.GetInstalledExtensions())
            {
                result.Add(new InstalledExtensionRef(name, version, location));
                if (location is not null)
                {
                    _loaderByLocation[location] = loader;
                }
            }
        }

        return result.Count > 0 ? result : null;
    }
}
