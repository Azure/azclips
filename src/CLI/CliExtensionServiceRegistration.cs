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
        var logPath = Path.Combine(Path.GetTempPath(), "cirrus-discovery.log");
        File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] BuildLeafIndexWithDiscovery called\n");
        
        var registryStore = new FileExtensionRegistryStore();
        var fileRegistry = registryStore.Load();
        File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] File registry has {fileRegistry.Extensions.Count} extensions\n");

        // §5.3: Discover all installed extensions from disk. This is authoritative.
        var discoveredRegistry = DiscoverInstalledExtensions(fileRegistry);
        File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] Discovered registry has {discoveredRegistry.Extensions.Count} extensions\n");

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
        var logPath = Path.Combine(Path.GetTempPath(), "cirrus-discovery.log");
        try
        {
            var engine = CirrusServiceProvider.GetRequiredService<IEngine>();
            var discovered = new ExtensionRegistry { SchemaVersion = ExtensionRegistry.CurrentSchemaVersion };

            // Open a transient discovery session to enumerate all installed extensions.
            // Output is discarded; we only care about the emitted DiscoveredExtensionInfo.
            var outputStreams = new OutputStreams(
                WriteOutput: async (result, _) =>
                {
                    var msg = $"[{DateTime.UtcNow:O}] OutputStreams.WriteOutput called with result type: {result?.GetType().Name}";
                    File.AppendAllText(logPath, msg + Environment.NewLine);
                    
                    // Capture discovered extensions and convert to registry entries.
                    if (result is List<DiscoveredExtensionInfo> infos)
                    {
                        var countMsg = $"[{DateTime.UtcNow:O}] Got {infos.Count} discovered extensions";
                        File.AppendAllText(logPath, countMsg + Environment.NewLine);
                        
                        foreach (var info in infos)
                        {
                            var pkgMsg = $"[{DateTime.UtcNow:O}] Processing: {info.PackageId} v{info.ResolvedVersion}";
                            File.AppendAllText(logPath, pkgMsg + Environment.NewLine);
                            
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
                                var verbMsg = $"[{DateTime.UtcNow:O}]   Verb: {verb}";
                                File.AppendAllText(logPath, verbMsg + Environment.NewLine);
                            }

                            discovered.Extensions[info.PackageId] = entry;
                        }
                    }
                    else
                    {
                        var typeMsg = $"[{DateTime.UtcNow:O}] Result is NOT List<DiscoveredExtensionInfo>, got {result?.GetType().FullName ?? "null"}";
                        File.AppendAllText(logPath, typeMsg + Environment.NewLine);
                    }
                    await Task.CompletedTask;
                },
                WriteVerbose: async (message, _) => { await Task.CompletedTask; },
                WriteDebug: async (message, _) => { await Task.CompletedTask; },
                WriteWarning: async (message, _) => { await Task.CompletedTask; },
                WriteError: async (message, _) => { await Task.CompletedTask; },
                WriteInformation: async (message, _) => { await Task.CompletedTask; });

            var sessionMsg = $"[{DateTime.UtcNow:O}] Opening discovery session...";
            File.AppendAllText(logPath, sessionMsg + Environment.NewLine);
            
            using var session = engine.OpenDiscoverySession(
                extensionNames: new List<string>(),  // Empty = all extensions
                commandName: null,
                outputStreams: outputStreams,
                isTransient: true);

            var runMsg = $"[{DateTime.UtcNow:O}] Running discovery session...";
            File.AppendAllText(logPath, runMsg + Environment.NewLine);
            
            session.RunAsync().GetAwaiter().GetResult();
            
            var doneMsg = $"[{DateTime.UtcNow:O}] Discovery complete, got {discovered.Extensions.Count} extensions";
            File.AppendAllText(logPath, doneMsg + Environment.NewLine);
            
            return discovered;
        }
        catch (Exception ex)
        {
            // Discovery failure: fall back to file registry. This preserves existing behavior
            // while allowing graceful degradation if discovery fails.
            var errMsg = $"[{DateTime.UtcNow:O}] DISCOVERY FAILED: {ex.GetType().Name}: {ex.Message}";
            File.AppendAllText(logPath, errMsg + Environment.NewLine);
            File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] Stack trace: {ex.StackTrace}" + Environment.NewLine);
            if (ex.InnerException != null)
            {
                var innerMsg = $"[{DateTime.UtcNow:O}] Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
                File.AppendAllText(logPath, innerMsg + Environment.NewLine);
                File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] Inner stack: {ex.InnerException.StackTrace}" + Environment.NewLine);
            }
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
