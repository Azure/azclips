// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli;

using System.Reflection;
using Core.Abstractions;
using Core.Extensions;

public class CLIAssemblyLoadingProvider : IAssemblyLoadingProvider
{
    private readonly IEnumerable<IExtensionLoader> extensionLoaders;
    private readonly ILoggingService loggingService;
    private readonly IExtensionRegistryStore? registryStore;

    public CLIAssemblyLoadingProvider(
        IEnumerable<IExtensionLoader> extensionLoaders,
        ILoggingService loggingService,
        IExtensionRegistryStore? registryStore = null)
    {
        this.extensionLoaders = extensionLoaders;
        this.loggingService = loggingService;
        this.registryStore = registryStore;

        // One-time chain-order log so `--debug` output explains "which loader ran first".
        loggingService.Debug($"[{nameof(CLIAssemblyLoadingProvider)}] Loader chain: [{string.Join(", ", extensionLoaders.Select(l => l.GetType().Name))}].");
    }

    private Assembly? LoadExtension(IEnumerable<IExtensionLoader> extensionLoaders, string extensionName, string? version = null) => extensionLoaders
                    .Select(loader => loader.LoadExtension(extensionName, version))
                    .FirstOrDefault(assembly => assembly != null);

    public Assembly LoadExtension(string extensionName, string? extensionVersion = null)
    {
        loggingService.Info($"Processing command with extension '{extensionName}'.");
        return LoadExtension(extensionLoaders, extensionName, extensionVersion) ?? throw new InvalidOperationException($"Extension '{extensionName}' could not be found by any extension loader.");
    }

    public IList<InstalledExtensionRef>? GetAllInstalledExtensions()
    {
        var result = new List<InstalledExtensionRef>();
        var registry = TryLoadRegistry();

        foreach (var loader in extensionLoaders)
        {
            foreach (var (name, version) in loader.GetInstalledExtensions())
            {
                var effectiveVersion = version;
                if (ShouldOverrideWithPinnedVersion(loader, version, registry, name, out var pinnedVersion))
                {
                    effectiveVersion = pinnedVersion;
                }

                result.Add(new InstalledExtensionRef(name, effectiveVersion));
            }
        }

        return result.Count > 0 ? result : null;
    }

    private ExtensionRegistry? TryLoadRegistry()
    {
        try
        {
            return (registryStore ?? new FileExtensionRegistryStore()).Load();
        }
        catch (Exception ex)
        {
            loggingService.Debug($"Failed to load extension registry during discovery: {ex.Message}");
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
            loader is BuiltinExtensionLoader ||
            registry is null ||
            !RegistryLookup.TryFind(registry, extensionName, out var match) ||
            !match.Value.Pinned ||
            string.IsNullOrWhiteSpace(match.Value.Version))
        {
            return false;
        }

        if (loader is MultiVersionExtensionLoader multiVersionLoader &&
            !multiVersionLoader.HasInstalledVersion(extensionName, match.Value.Version))
        {
            return false;
        }

        pinnedVersion = match.Value.Version;
        return true;
    }
}