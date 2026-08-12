// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Extensions;

namespace Microsoft.Azure.Azclips.Extensions.Extension.Processors;

public class UpdateExtensionProcessor : IProcessor
{
    private OutputStreamDelegate _writeOutput = null!;

    [ArgumentInfo(
    Required = true,
    Description = @"Package id (full or short suffix) of the extension to update. Verb and namespace tokens are not accepted.",
    SerializedName = @"Name")]
    public string Name { get; set; } = string.Empty;

    [ArgumentInfo(
    Required = false,
    Description = @"Target version. Defaults to the latest available from the configured feed.",
    SerializedName = @"Version")]
    public string? Version { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"Feed URL to install the new version from.",
    SerializedName = @"FeedUrl")]
    public string? FeedUrl { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"Update even if the extension is pinned.",
    SerializedName = @"Force")]
    public bool Force { get; set; }

    public IProcessor Init(ProcessorContext context)
    {
        _writeOutput = context.OutputHandler.WriteOutput;
        return this;
    }

    public async Task ProcessAsync()
    {
        var result = Process();
        await _writeOutput(result);
    }

    public object Process()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw AzclipsErrorException.UsageError("Name is required to update an extension.");
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        var loggingService = AzclipsServiceProvider.GetRequiredService<ILoggingService>();
        using var activity = telemetryService.StartActivity($"{nameof(UpdateExtensionProcessor)}.{nameof(Process)}");

        var store = AzclipsServiceProvider.GetRequiredService<IExtensionRegistryStore>();
        var registry = store.Load();

        if (!RegistryLookup.TryFind(registry, Name, out var match))
        {
            var message = $"No installed extension matches '{Name}' (by package id or short name). Use 'extension add' first.";
            if (RegistryLookup.SuggestPackageForToken(registry, Name, out var suggested))
            {
                message = $"No installed extension matches '{Name}' (by package id or short name). " +
                          $"'{Name}' is a verb contributed by package '{suggested}'; to update that package use '--name {RegistryLookup.ToShortName(suggested)}'.";
            }
            throw AzclipsErrorException.ExtensionNotFound(
                message,
                new Dictionary<string, object?> { ["name"] = Name });
        }

        var packageId = match.Key;
        var existing = match.Value;

        // Tag activity with pre-update snapshot; post-install reads below refresh to the new shape.
        activity?.SetTag("extension.package_id", packageId);
        activity?.SetTag("extension.version", existing.Version);
        activity?.SetTag("extension.source", (FeedUrl ?? existing.Source) ?? "path");

        // Pinning blocks update unless --force is passed.
        if (existing.Pinned && !Force)
        {
            throw AzclipsErrorException.PinViolation(
                $"Extension '{packageId}' is pinned at version '{existing.Version}'. Pass --force to override the pin.",
                new Dictionary<string, object?>
                {
                    ["packageId"] = packageId,
                    ["pinnedVersion"] = existing.Version,
                });
        }

        var installer = new ExtensionInstaller(loggingService, feedUrl: FeedUrl ?? existing.Source);
        var installPath = installer.InstallExtensionAsync(
            packageId,
            Version,
            includePrerelease: true,
            includeUnlisted: false,
            CancellationToken.None
        ).GetAwaiter().GetResult();

        try
        {
            var verbs = ExtensionVerbReader.ReadVerbs(installPath);
            if (verbs.Count == 0)
            {
                throw AzclipsErrorException.VerbConflict(
                    $"Updated package '{packageId}' does not declare an [ExtensionVerb] attribute.",
                    new Dictionary<string, object?> { ["packageId"] = packageId });
            }
            activity?.SetTag("extension.namespace_count", verbs.Count);

            // Prefer v2 Namespaces; fall back to the legacy single Verb field.
            var existingVerbs = existing.Namespaces.Count > 0
                ? new HashSet<string>(existing.Namespaces, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(
                    existing.Verb is null ? Array.Empty<string>() : new[] { existing.Verb },
                    StringComparer.OrdinalIgnoreCase);
            var newVerbSet = new HashSet<string>(verbs, StringComparer.OrdinalIgnoreCase);

            // Block namespace changes across versions to prevent silent command-surface rebinding.
            if (!existingVerbs.SetEquals(newVerbSet))
            {
                throw AzclipsErrorException.VerbConflict(
                    $"Updated package '{packageId}' declares verbs [{string.Join(", ", verbs)}], " +
                    $"but the previously installed version owned [{string.Join(", ", existingVerbs)}]. " +
                    $"Refusing to silently rebind. Run 'extension remove {packageId}' then 'extension add' to change verbs.",
                    new Dictionary<string, object?>
                    {
                        ["packageId"] = packageId,
                        ["newVerbs"] = verbs,
                        ["existingVerbs"] = existingVerbs.ToArray(),
                    });
            }

            var primaryVerb = existing.Verb ?? verbs[0];

            // Recover the resolved version from the install path layout: {root}/{pkgId}/{version}/
            var resolvedVersion = new DirectoryInfo(installPath).Name;
            activity?.SetTag("extension.version", resolvedVersion);

            // Refresh Contributions; self-update can add/remove sub-leaves across minor versions.
            var leafKeys = LeafReader.ReadCliLeafKeys(installPath);
            activity?.SetTag("extension.contribution_count", leafKeys.Count);
            // File registry only carries preferences (contributions are stripped on Save); overlay
            // live discovery so PlanContributions can see already-installed leaves and detect collisions.
            var planningRegistry = DiscoveryBackedExtensionRegistry.BuildDiscoveredWithPreferences(registry);
            var contributions = new List<Contribution>(
                ExtensionAddPlanner.PlanContributions(packageId, verbs, leafKeys, planningRegistry));

            // Strict single-version: only the just-installed version stays on disk. Persist
            // before purge — if Save fails (e.g. concurrency conflict), the previous payload
            // survives and the registry still points at it.
            var newEntry = new ExtensionRegistryEntry
            {
                Version = resolvedVersion,
                // Forced update breaks the pin (the version it pinned no longer exists on disk).
                Pinned = existing.Pinned && !Force,
                Source = FeedUrl ?? existing.Source,
                InstalledAt = DateTimeOffset.UtcNow,
            };
            PersistAndPurge(
                store,
                registry,
                packageId,
                newEntry,
                () => PurgeSiblingVersions(installer.InstallPath, packageId, keepVersion: resolvedVersion));

            return new
            {
                PackageId = packageId,
                Verb = primaryVerb,
                Verbs = verbs,
                PreviousVersion = existing.Version,
                Version = resolvedVersion,
                Pinned = existing.Pinned && !Force,
                Updated = true,
            };
        }
        catch
        {
            TryDeleteDirectory(installPath);
            throw;
        }
    }

    public string Process(bool Serialize)
    {
        var result = Process();
        if (Serialize)
        {
            return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        else
        {
            return result.ToString() ?? "";
        }
    }

    /// <summary>
    /// Atomically replaces the registry entry and persists via <paramref name="store"/>, then
    /// runs <paramref name="purgeAction"/> only if <c>Save</c> succeeded — a failed save leaves
    /// the prior payload intact. Extracted for unit testing.
    /// </summary>
    public static void PersistAndPurge(
        IExtensionRegistryStore store,
        ExtensionRegistry registry,
        string packageId,
        ExtensionRegistryEntry newEntry,
        Action purgeAction)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(newEntry);
        ArgumentNullException.ThrowIfNull(purgeAction);

        registry.Extensions[packageId] = newEntry;
        store.Save(registry);
        purgeAction();
    }

    private static void PurgeSiblingVersions(string installRoot, string packageId, string keepVersion)
    {
        var packageDir = Path.Combine(installRoot, packageId);
        if (!Directory.Exists(packageDir))
        {
            return;
        }

        foreach (var versionDir in Directory.GetDirectories(packageDir))
        {
            var versionName = Path.GetFileName(versionDir);
            if (string.Equals(versionName, keepVersion, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            TryDeleteDirectory(versionDir);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
