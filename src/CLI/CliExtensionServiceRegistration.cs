// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable CS0618 // Obsolete warnings: will be replaced with DiscoverySession in §5.3

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Cli.Parser;
using Core.DependencyInjection;
using Core.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Cli;

/// <summary>
/// Registers verb-to-package resolution as DI services so hosts (and tests/benchmarks) can supply a
/// scoped registry store instead of the hard-coded ~/.cirrus default.
/// </summary>
public static class CliExtensionServiceRegistration
{
    private static readonly HashSet<string> s_builtInPackageIds = BuiltInExtensions.Map.Values
        .Select(static value => value.PackageId)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static IServiceCollection AddCliExtensionDispatch(this IServiceCollection services)
    {
        services.AddSingleton<IExtensionRegistryStore>(
            _ => new ReconcilingRegistryStore(new FileExtensionRegistryStore()));

        services.AddSingleton<IExtensionResolver>(
            sp => new ExtensionResolver(sp.GetRequiredService<IExtensionRegistryStore>()));

        services.AddSingleton(sp => new ExtensionDispatcher(
            sp.GetRequiredService<IExtensionResolver>(),
            BuildLeafIndexWithDiscovery));

        return services;
    }

    /// <summary>
    /// Builds LeafIndex from both discovered extensions (authoritative) and registry preferences.
    /// Discovery is now the source of truth for installed state; registry provides preference overlays.
    /// §5.3: Routes verbs from disk even if registry.json is missing.
    /// </summary>
    private static LeafIndex BuildLeafIndexWithDiscovery()
    {
        var registryStore = new FileExtensionRegistryStore();
        var fileRegistry = registryStore.Load();

        // §5.3: Discover all installed extensions from disk. This is authoritative.
        var discoveredRegistry = DiscoverInstalledExtensions(fileRegistry);

        // Merge discovered extensions with file registry, preserving preferences from file.
        MergePreferencesFromFileRegistry(discoveredRegistry, fileRegistry);

        return LeafIndex.Build(BuiltInExtensions.Map, discoveredRegistry);
    }

    /// <summary>
    /// Runs DiscoverySession to enumerate installed extensions and construct an ExtensionRegistry
    /// from discovered verbs. This is the authoritative source of installed state.
    /// </summary>
    private static ExtensionRegistry DiscoverInstalledExtensions(ExtensionRegistry currentRegistry)
    {
        try
        {
            var engine = CirrusServiceProvider.GetRequiredService<IEngine>();
            
            var discovered = new ExtensionRegistry { SchemaVersion = ExtensionRegistry.CurrentSchemaVersion };

            // Open a transient discovery session to enumerate all installed extensions.
            // Output is discarded; we only care about the emitted DiscoveredExtensionInfo.
            var outputStreams = new OutputStreams(
                WriteOutput: async (result, _) =>
                {
                    // Capture discovered extensions and convert to registry entries.
                    if (result is List<DiscoveredExtensionInfo> infos)
                    {
                        foreach (var info in infos)
                        {
                            if (s_builtInPackageIds.Contains(info.PackageId))
                            {
                                continue;
                            }

                            // Each discovered extension gets one entry with Contributions built from Verbs.
                            var entry = new ExtensionRegistryEntry
                            {
                                Version = info.ResolvedVersion,
                                Pinned = false,
                                Source = null,
                                InstalledAt = DateTimeOffset.UtcNow,
                            };

                            // Build contributions from discovered verbs:
                            // Each verb contributes a root entry (verb, "").
                            foreach (var verb in info.Verbs)
                            {
                                entry.Contributions.Add(new Contribution { Verb = verb, Path = string.Empty });
                            }

                            discovered.Extensions[info.PackageId] = entry;
                        }
                    }

                    await Task.CompletedTask;
                },
                WriteVerbose: async (message, _) => { await Task.CompletedTask; },
                WriteDebug: async (message, _) => { await Task.CompletedTask; },
                WriteWarning: async (message, _) => { await Task.CompletedTask; },
                WriteError: async (message, _) => { await Task.CompletedTask; },
                WriteInformation: async (message, _) => { await Task.CompletedTask; });

            using var session = engine.OpenDiscoverySession(
                extensionNames: new List<string>(),  // Empty = all extensions
                commandName: null,
                outputStreams: outputStreams,
                isTransient: true);
            
            session.RunAsync().GetAwaiter().GetResult();

            return discovered;
        }
        catch
        {
            // Discovery failure: fall back to file registry. This preserves existing behavior
            // while allowing graceful degradation if discovery fails.
            return currentRegistry;
        }
    }

    /// <summary>
    /// Overlays preferences from the file registry (pinned, source, installedAt) onto discovered entries.
    /// This ensures user pins and metadata are preserved even when discovery re-runs.
    /// </summary>
    private static void MergePreferencesFromFileRegistry(
        ExtensionRegistry discoveredRegistry,
        ExtensionRegistry fileRegistry)
    {
        foreach (var (packageId, fileEntry) in fileRegistry.Extensions)
        {
            if (discoveredRegistry.Extensions.TryGetValue(packageId, out var discovered))
            {
                // Preserve preferences from file: pinned state, source, install timestamp.
                discovered.Pinned = fileEntry.Pinned;
                discovered.Source = fileEntry.Source;
                discovered.InstalledAt = fileEntry.InstalledAt;
            }
        }
    }
}
