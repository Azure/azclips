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
            foreach (var info in loader.GetInstallations())
            {
                if (!ShouldOverrideWithPinnedVersion(loader, info.Version, registry, info.Name, out var pinnedVersion) ||
                    string.Equals(pinnedVersion, info.Version, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(info);
                }
                else if (loader is InstalledExtensionLoader installedLoader)
                {
                    result.Add(installedLoader.ResolveInstallation(info.Name, pinnedVersion) ?? info);
                }
                else if (info.Installation is not null)
                {
                    result.Add(info);
                }
                else
                {
                    // Legacy loaders cannot resolve a concrete pinned locator.
                    result.Add(new InstalledExtensionRef(info.Name, pinnedVersion, null));
                }
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

        pinnedVersion = match.Value.Version;
        return true;
    }
}