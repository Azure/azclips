// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli;

using System.Reflection;
using Core.Abstractions;
using Core.Extensions;

public class CLIAssemblyLoadingProvider(
    IEnumerable<IExtensionLoader> extensionLoaders,
    ILoggingService loggingService,
    IExtensionRegistryStore? registryStore = null) : IAssemblyLoadingProvider
{
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

        ExtensionRegistry? registry = null;
        try
        {
            registry = (registryStore ?? new FileExtensionRegistryStore()).Load();
        }
        catch
        {
            // Discovery is best-effort; if registry read fails, keep loader defaults.
        }

        foreach (var loader in extensionLoaders)
        {
            foreach (var (name, version) in loader.GetInstalledExtensions())
            {
                var effectiveVersion = version;

                // If the package is pinned in registry.json, force discovery/load to that exact
                // installed version instead of the loader's default semver-latest choice.
                if (!string.IsNullOrWhiteSpace(version) &&
                    loader is not BuiltinExtensionLoader &&
                    registry is not null &&
                    RegistryLookup.TryFind(registry, name, out var match) &&
                    match.Value.Pinned &&
                    !string.IsNullOrWhiteSpace(match.Value.Version))
                {
                    effectiveVersion = match.Value.Version;
                }

                result.Add(new InstalledExtensionRef(name, effectiveVersion));
            }
        }

        return result.Count > 0 ? result : null;
    }
}