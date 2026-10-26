// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;
using Core.Cli.Parser;
using Core.Configuration.Options;
using Core.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Cli;

/// <summary>
/// Registers verb-to-package resolution as DI services so hosts (and tests/benchmarks) can supply a
/// scoped registry store instead of the hard-coded ~/.azclips default.
/// </summary>
public static class CliExtensionServiceRegistration
{
    public static IServiceCollection AddCliExtensionDispatch(this IServiceCollection services)
    {
        services.AddSingleton<IExtensionRegistryStore>(sp =>
        {
            var config = sp.GetRequiredService<IConfigurationService>();
            var root = config.GetConfiguration<AssemblyLoadingOptions>().InstalledExtensionsRoot;
            return new FileExtensionRegistryStore(Path.Combine(root, HomePaths.RegistryFileName));
        });

        services.AddSingleton<IExtensionResolver>(
            sp => new ExtensionResolver(sp.GetRequiredService<IExtensionRegistryStore>()));

        services.AddSingleton(sp => new ExtensionRoutingDiscovery(
            sp.GetRequiredService<IAssemblyLoadingService>(),
            sp.GetService<ILoggingService>() ?? NoOpLoggingService.Instance));

        services.AddSingleton(sp => new ExtensionDispatcher(
            sp.GetRequiredService<IExtensionResolver>(),
            () => BuildLeafIndexWithDiscovery(
                sp.GetRequiredService<IExtensionRegistryStore>(),
                sp.GetService<ILoggingService>() ?? NoOpLoggingService.Instance,
                () => sp.GetRequiredService<ExtensionRoutingDiscovery>().Discover()),
            sp.GetService<ILoggingService>()));

        return services;
    }

    private sealed class NoOpLoggingService : ILoggingService
    {
        internal static readonly NoOpLoggingService Instance = new();

        public void Log(string message) { }
        public void Info(string message) { }
        public void Verbose(string message) { }
        public void Debug(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }

    /// <summary>
    /// Builds LeafIndex from both discovered extensions (authoritative) and registry preferences.
    /// Discovery is now the source of truth for installed state; registry provides preference overlays.
    /// §5.3: Routes verbs from disk even if registry.json is missing.
    /// </summary>
    private static LeafIndex BuildLeafIndexWithDiscovery(
        IExtensionRegistryStore registryStore,
        ILoggingService loggingService,
        Func<IReadOnlyList<ExtensionRoutingInfo>> discover)
    {
        var timer = Stopwatch.StartNew();
        var fileRegistry = registryStore.Load();

        // §5.3: Discover all installed extensions from disk. This is authoritative.
        var discoveredRegistry = DiscoverInstalledExtensions(fileRegistry, loggingService, discover, out var discoverySucceeded);

        // Merge discovered extensions with file registry, preserving preferences from file.
        MergePreferencesFromFileRegistry(discoveredRegistry, fileRegistry);

        var indexTimer = Stopwatch.StartNew();
        var index = LeafIndex.Build(discoveredRegistry, isAuthoritative: discoverySucceeded);
        loggingService.Debug($"[ExtensionRoutingDiscovery] Built routing index for {discoveredRegistry.Extensions.Count} packages in {indexTimer.Elapsed.TotalMilliseconds:F1}ms; total dispatch discovery={timer.Elapsed.TotalMilliseconds:F1}ms.");
        return index;
    }

    /// <summary>
    /// Constructs the routing registry from sidecars with per-extension DLL fallback.
    /// </summary>
    private static ExtensionRegistry DiscoverInstalledExtensions(
        ExtensionRegistry currentRegistry,
        ILoggingService loggingService,
        Func<IReadOnlyList<ExtensionRoutingInfo>> discover,
        out bool discoverySucceeded)
    {
        discoverySucceeded = false;
        IReadOnlyList<ExtensionRoutingInfo> infos;
        try
        {
            infos = discover();
            discoverySucceeded = true;
        }
        catch (Exception ex)
        {
            // Best-effort fallback for degraded environments where discovery cannot run.
            loggingService.Warn($"[ExtensionRoutingDiscovery] Enumeration failed; falling back to the current registry: {ex.Message}");
            return currentRegistry;
        }

        var discovered = new ExtensionRegistry { SchemaVersion = ExtensionRegistry.CurrentSchemaVersion };
        var timer = Stopwatch.StartNew();
        foreach (var info in SelectInstalledCandidates(infos, currentRegistry, loggingService))
        {
            var projection = BuildContributionProjection(info);
            discovered.Extensions[info.PackageId] = new ExtensionRegistryEntry
            {
                Version = info.ResolvedVersion,
                AssemblyReference = info.AssemblyReference,
                Location = info.Location,
                Contributions = projection.Contributions,
                Namespaces = projection.Namespaces,
                InstalledAt = DateTimeOffset.UtcNow,
            };
        }
        loggingService.Debug($"[ExtensionRoutingDiscovery] Selected {discovered.Extensions.Count} packages from {infos.Count} candidates in {timer.Elapsed.TotalMilliseconds:F1}ms.");
        return discovered;
    }

    private static IEnumerable<ExtensionRoutingInfo> SelectInstalledCandidates(
        IEnumerable<ExtensionRoutingInfo> infos,
        ExtensionRegistry currentRegistry,
        ILoggingService loggingService)
    {
        foreach (var group in infos.GroupBy(static info => info.PackageId, StringComparer.OrdinalIgnoreCase))
        {
            currentRegistry.Extensions.TryGetValue(group.Key, out var persistedPreference);
            var selection = ExtensionCandidateSelector.Select(group.ToList(), persistedPreference);
            loggingService.Debug(ExtensionCandidateSelector.BuildDiagnostics(
                context: "CliExtensionServiceRegistration.SelectInstalledCandidates",
                selection: selection,
                persistedPreference: persistedPreference));

            ExtensionCandidateSelector.ReportUnresolvablePin(group.Key, selection, persistedPreference, loggingService);

            yield return selection.Selected;
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

    internal static (List<Contribution> Contributions, List<string> Namespaces) BuildContributionProjection(DiscoveredExtensionInfo info)
        => BuildContributionProjection(ExtensionRoutingInfo.FromDiscovery(info));

    internal static (List<Contribution> Contributions, List<string> Namespaces) BuildContributionProjection(ExtensionRoutingInfo info)
    {
        var seenLeaves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var contributions = new List<Contribution>();

        foreach (var key in info.Leaves)
        {
            if (!TryParseInvocationKey(key, out var verb, out var path))
            {
                continue;
            }

            if (!seenLeaves.Add(ToLeafKey(verb, path)))
            {
                continue;
            }

            contributions.Add(new Contribution { Verb = verb, Path = path });
        }

        // Fallback for commandless indexes: retain root ownership from advertised verbs.
        if (contributions.Count == 0)
        {
            foreach (var verb in info.Verbs)
            {
                var normalizedVerb = (verb ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(normalizedVerb))
                {
                    continue;
                }

                if (!seenLeaves.Add(ToLeafKey(normalizedVerb, string.Empty)))
                {
                    continue;
                }

                contributions.Add(new Contribution { Verb = normalizedVerb, Path = string.Empty });
            }
        }

        var namespaces = contributions
            .Select(c => c.Verb)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (contributions, namespaces);
    }

    private static bool TryParseInvocationKey(string? key, out string verb, out string path)
    {
        verb = string.Empty;
        path = string.Empty;

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var normalized = key.Trim();
        var firstSpace = normalized.IndexOf(' ');
        if (firstSpace < 0)
        {
            verb = normalized;
            path = string.Empty;
            return true;
        }

        verb = normalized[..firstSpace].Trim();
        path = normalized[(firstSpace + 1)..].Trim();
        return !string.IsNullOrEmpty(verb);
    }

    private static string ToLeafKey(string verb, string path) => $"{verb}\u0000{path}";
}
