using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Extensions;

namespace Microsoft.Azure.Cirrus.Extensions.Extension.Processors;

public class UpdateExtensionProcessor : IProcessor
{
    private OutputStreamDelegate _writeOutput = null!;

    [ArgumentInfo(
    Required = true,
    Description = @"Package id or verb of the extension to update.",
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
            throw CirrusErrorException.UsageError("Name is required to update an extension.");
        }

        var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
        var loggingService = CirrusServiceProvider.GetRequiredService<ILoggingService>();
        using var activity = telemetryService.StartActivity($"{nameof(UpdateExtensionProcessor)}.{nameof(Process)}");

        var store = new FileExtensionRegistryStore();
        var registry = store.Load();

        if (!RegistryLookup.TryFind(registry, Name, out var match))
        {
            throw CirrusErrorException.ExtensionNotFound(
                $"No installed extension matches '{Name}' (by package id, short name, verb, or namespace). Use 'extension add' first.",
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
            throw CirrusErrorException.PinViolation(
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
                throw CirrusErrorException.VerbConflict(
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
                throw CirrusErrorException.VerbConflict(
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
            var contributions = new List<Contribution>();
            foreach (var leafKey in leafKeys)
            {
                var (leafVerb, leafPath) = LeafReader.SplitLeafKey(leafKey);
                if (!newVerbSet.Contains(leafVerb))
                {
                    throw CirrusErrorException.LeafConflict(
                        $"Updated package '{packageId}' contributes leaf '{leafKey}' whose first token is '{leafVerb}', " +
                        $"but only verbs [{string.Join(", ", verbs)}] are declared via [ExtensionVerb]. " +
                        $"Every s_cliTable key must start with a declared verb.",
                        new Dictionary<string, object?>
                        {
                            ["packageId"] = packageId,
                            ["declaredVerbs"] = verbs,
                            ["leafKey"] = leafKey,
                            ["leafVerb"] = leafVerb,
                        });
                }
                contributions.Add(new Contribution { Verb = leafVerb, Path = leafPath });
            }

            // Q6 strict single-version: only the just-installed version stays on disk. Order
            // matters: persist the registry first, then purge old payload directories. If
            // PurgeSiblingVersions ran before Save() and the Save then failed (e.g. concurrency
            // conflict), we'd have deleted the previous-version files but the registry would
            // still point at the old version — leaving the user with neither a working old
            // install nor a recorded new install. (PR #253 review feedback.)
            var newEntry = new ExtensionRegistryEntry
            {
                Verb = primaryVerb,
                Version = resolvedVersion,
                // Forced update breaks the pin (the version it pinned no longer exists on disk).
                Pinned = existing.Pinned && !Force,
                Source = FeedUrl ?? existing.Source,
                InstalledAt = DateTimeOffset.UtcNow,
                Contributions = contributions,
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
    /// Atomically replace the registry entry, persist via <paramref name="store"/>, then run
    /// <paramref name="purgeAction"/>. Order matters: the purge runs only if <c>Save</c>
    /// succeeds. If <c>Save</c> throws (e.g. <see cref="CirrusErrorException"/> with code
    /// <c>RegistryConcurrency</c>), the purge is skipped so the previous-version payload remains
    /// on disk and the registry is unchanged. Extracted for unit testing — IO lives in
    /// <see cref="Process()"/>'s catch block. (PR #253 review feedback.)
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
