// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli;

using System.Reflection;
using Core.Abstractions;
using Core.Extensions;

public class CLIAssemblyLoadingProvider : IAssemblyLoadingProvider
{
    private readonly IEnumerable<IExtensionLoader> _extensionLoaders;
    private readonly ILoggingService _loggingService;
    private readonly IExtensionRegistryStore _registryStore;

    public CLIAssemblyLoadingProvider(
        IEnumerable<IExtensionLoader> extensionLoaders,
        ILoggingService loggingService,
        IExtensionRegistryStore registryStore)
    {
        _extensionLoaders = extensionLoaders;
        _loggingService = loggingService;
        _registryStore = registryStore;
    }

    private Assembly? LoadExtensionFromLoaders(string extensionName, string? version = null) => _extensionLoaders
                    .Select(loader => loader.LoadExtension(extensionName, version))
                    .FirstOrDefault(assembly => assembly != null);

    public Assembly LoadExtension(string extensionName, string? extensionVersion = null)
    {
        _loggingService.Info($"Processing command with extension '{extensionName}'.");
        return LoadExtensionFromLoaders(extensionName, extensionVersion) ?? throw new InvalidOperationException($"Extension '{extensionName}' could not be found by any extension loader.");
    }

    public IList<InstalledExtensionRef>? GetAllInstalledExtensions()
    {
        var result = new List<InstalledExtensionRef>();
        var registry = TryLoadRegistry();

        foreach (var loader in _extensionLoaders)
        {
            foreach (var (name, version, location) in loader.GetInstalledExtensions())
            {
                var effectiveVersion = version;
                var effectiveLocation = location;
                if (ShouldOverrideWithPinnedVersion(loader, version, registry, name, out var pinnedVersion) &&
                    !string.Equals(pinnedVersion, version, StringComparison.OrdinalIgnoreCase))
                {
                    effectiveVersion = pinnedVersion;
                    // Pin routes to a different version than what the loader discovered; the
                    // reported Location points at the discovered version's DLL, not the pinned
                    // one. Null it out rather than returning a path that disagrees with dispatch.
                    // When the pin matches the discovered version, keep Location intact —
                    // dispatch loads that exact DLL.
                    effectiveLocation = null;
                }

                result.Add(new InstalledExtensionRef(name, effectiveVersion, effectiveLocation));
            }
        }

        return result.Count > 0 ? result : null;
    }

    private ExtensionRegistry? TryLoadRegistry()
    {
        try
        {
            return _registryStore.Load();
        }
        catch (Exception ex)
        {
            _loggingService.Debug($"Failed to load extension registry during discovery: {ex.Message}");
            return null;
        }
    }

    private static bool ShouldOverrideWithPinnedVersion(
        IExtensionLoader loader,
        string? discoveredVersion,
        ExtensionRegistry? registry,
        string extensionName,
        out string pinnedVersion)
    {
        pinnedVersion = string.Empty;

        if (string.IsNullOrWhiteSpace(discoveredVersion) ||
            loader is BundledExtensionLoader ||
            registry is null ||
            !RegistryLookup.TryFind(registry, extensionName, out var match) ||
            !match.Value.Pinned ||
            string.IsNullOrWhiteSpace(match.Value.Version))
        {
            return false;
        }

        if (loader is InstalledExtensionLoader installedLoader &&
            !installedLoader.HasInstalledVersion(extensionName, match.Value.Version))
        {
            return false;
        }

        pinnedVersion = match.Value.Version;
        return true;
    }
}