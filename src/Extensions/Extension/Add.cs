using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Extensions;

namespace Microsoft.Azure.Cirrus.Extensions.Extension.Processors;

public class AddExtensionProcessor : IProcessor
{
    private OutputStreamDelegate _writeOutput = null!;

    [ArgumentInfo(
    Required = false,
    Description = @"The name of the extension to add.",
    SerializedName = @"Name")]
    public string? Name { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"The version of the extension to add.",
    SerializedName = @"Version")]
    public string? Version { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"Feed URL to install the extension from.",
    SerializedName = @"FeedUrl")]
    public string? FeedUrl { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"Path to the extension .nupkg file to add.",
    SerializedName = @"Path")]
    public string? Path { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"Pin the installed version so future 'update' commands skip it without --force.",
    SerializedName = @"Pin")]
    public bool Pin { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"Allow add to overwrite an existing pinned installation of the same package.",
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
        if (string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Path))
        {
            throw CirrusErrorException.UsageError("Either Name or Path must be provided to add an extension.");
        }

        var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
        var loggingService = CirrusServiceProvider.GetRequiredService<ILoggingService>();
        using var activity = telemetryService.ActivitySource?.StartActivity($"{nameof(AddExtensionProcessor)}.{nameof(Process)}");

        // Phase 10 #32: tag the activity with extension-shaped dimensions as soon as each value
        // becomes known so failed installs still carry whatever was determined before the throw
        // (e.g. a LeafConflict mid-iteration carries package_id/version/namespace_count and the
        // *attempted* contribution_count). OTEL semantic convention: dotted namespace, snake_case
        // attribute names. `extension.source` distinguishes feed installs from local-path side-
        // loads ("path" sentinel) for dev-loop vs production install ratio.
        activity?.SetTag("extension.source", FeedUrl ?? "path");

        var installer = new ExtensionInstaller(loggingService, feedUrl: FeedUrl);
        string installPath;
        if (!string.IsNullOrWhiteSpace(Name))
        {
            installPath = installer.InstallExtensionAsync(
                $"Microsoft.Azure.Cirrus.Extensions.{Name}",
                Version,
                includePrerelease: true,
                includeUnlisted: false,
                CancellationToken.None
            ).GetAwaiter().GetResult();
        }
        else
        {
            installPath = installer.InstallFromPackageAsync(Path!, CancellationToken.None).GetAwaiter().GetResult();
        }

        // Recover packageId and version from the install path.
        // Layout (set by ExtensionInstaller.InitializeExtensionFolder): {InstallPath}/{packageId}/{version}/
        var (packageId, version) = ParseInstallPath(installer.InstallPath, installPath);
        activity?.SetTag("extension.package_id", packageId);
        activity?.SetTag("extension.version", version);

        try
        {
            // Per docs/spec/extension-management.md §5.2: read the declared verb(s) from the
            // installed DLLs *without* executing extension code, then validate against built-ins +
            // registry before committing the registry write. Phase 9 makes [ExtensionVerb]
            // repeatable, so an extension may claim more than one namespace.
            var verbs = ExtensionVerbReader.ReadVerbs(installPath);
            if (verbs.Count == 0)
            {
                throw CirrusErrorException.VerbConflict(
                    $"Extension '{packageId}' does not declare an [ExtensionVerb] attribute. " +
                    $"Cirrus extensions must declare at least one top-level verb.",
                    new Dictionary<string, object?> { ["packageId"] = packageId });
            }
            activity?.SetTag("extension.namespace_count", verbs.Count);

            // First declared verb is the canonical "primary" for the v1 `verb` field; readers that
            // pre-date the multi-namespace model continue to see something sensible.
            var primaryVerb = verbs[0];

            // Built-in collision check (§4.2): a built-in verb is non-overridable.
            foreach (var v in verbs)
            {
                if (BuiltInExtensions.IsBuiltInVerb(v))
                {
                    throw CirrusErrorException.VerbConflict(
                        $"Extension '{packageId}' wants to register verb '{v}', but '{v}' is reserved for a built-in extension and cannot be claimed by an installed extension.",
                        new Dictionary<string, object?> { ["packageId"] = packageId, ["verb"] = v });
                }
            }

            var store = new FileExtensionRegistryStore();
            var registry = store.Load();

            // Shape B: top-level verbs are shared. Multiple extensions may declare the same
            // [ExtensionVerb] (e.g. two extensions both contributing under `vm`); collisions are
            // resolved per-leaf below against the (verb, path) tuple. The per-verb ownership
            // gate that existed under Shape A has been removed — matches the meeting decision
            // (May 2026) to adopt the az-cli many-to-many model. Built-in verbs remain reserved
            // (see the IsBuiltInVerb check above) and built-in roots are still rejected per-leaf.
            var declaredVerbSet = new HashSet<string>(verbs, StringComparer.OrdinalIgnoreCase);

            // Shape B (§11.7): read the candidate's leaves (s_cliTable keys) from the just-
            // installed DLLs and conflict-check every (verb, path) tuple against the registry
            // and the built-in seed. This is finer-grained than the verb-only check above and
            // catches the cross-namespace case where two extensions share a verb but contribute
            // different paths (allowed) vs. the same path (rejected).
            var leafKeys = LeafReader.ReadCliLeafKeys(installPath);
            activity?.SetTag("extension.contribution_count", leafKeys.Count);
            var contributions = new List<Contribution>();
            foreach (var leafKey in leafKeys)
            {
                var (leafVerb, leafPath) = LeafReader.SplitLeafKey(leafKey);

                // The first token of every leaf key must match one of the declared [ExtensionVerb]
                // attributes (§11.2, Q10). For multi-namespace extensions any declared verb is
                // fair game — the build-time `ExtensionHints.targets` validation enforces the
                // same rule at PR time.
                if (!declaredVerbSet.Contains(leafVerb))
                {
                    throw CirrusErrorException.LeafConflict(
                        $"Extension '{packageId}' contributes leaf '{leafKey}' whose first token is '{leafVerb}', " +
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

                // Built-in collision: built-ins occupy (verb, "") only, so a leaf with empty path
                // for a built-in verb collides; the verb-level check above already caught this,
                // but be defensive.
                if (BuiltInExtensions.IsBuiltInVerb(leafVerb) && string.IsNullOrEmpty(leafPath))
                {
                    throw CirrusErrorException.LeafConflict(
                        $"Extension '{packageId}' contributes root leaf '{leafKey}', which is reserved for " +
                        $"the built-in '{leafVerb}' extension.",
                        new Dictionary<string, object?>
                        {
                            ["packageId"] = packageId,
                            ["leafKey"] = leafKey,
                        });
                }

                // Registry collision: another installed package already contributes the same
                // (verb, path). Self-upsert is allowed.
                foreach (var (existingPackageId, existingEntry) in registry.Extensions)
                {
                    if (string.Equals(existingPackageId, packageId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    foreach (var existingContribution in existingEntry.Contributions)
                    {
                        if (string.Equals(existingContribution.Verb, leafVerb, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(existingContribution.Path, leafPath, StringComparison.OrdinalIgnoreCase))
                        {
                            throw CirrusErrorException.LeafConflict(
                                $"Extension '{packageId}' wants to contribute leaf '{leafKey}', but it is already " +
                                $"owned by '{existingPackageId}'. Remove the existing extension first: " +
                                $"azv2 extension remove {existingPackageId}",
                                new Dictionary<string, object?>
                                {
                                    ["packageId"] = packageId,
                                    ["leafKey"] = leafKey,
                                    ["verb"] = leafVerb,
                                    ["path"] = leafPath,
                                    ["existingPackageId"] = existingPackageId,
                                });
                        }
                    }
                }

                contributions.Add(new Contribution { Verb = leafVerb, Path = leafPath });
            }

            // Per Q6 (strict single-version): purge any sibling version directories under the
            // package's parent dir so only the just-installed version remains on disk.
            PurgeSiblingVersions(installer.InstallPath, packageId, keepVersion: version);

            // Phase 10 #41: 'add' must respect a pin set on the *existing* entry. Pinning's
            // contract is "don't change my version under me"; without this check, a pinned
            // 1.1.0 could be silently overwritten by `add --path …1.0.0…`. Honor the pin
            // forward across the in-place upgrade — fail with PinViolation unless --force,
            // and carry the pin onto the new entry when the user explicitly forces.
            bool carryPinForward = CheckPinPrecondition(registry, packageId, version, Force, Pin);

            registry.Extensions[packageId] = new ExtensionRegistryEntry
            {
                Verb = primaryVerb,
                Version = version,
                Pinned = Pin || carryPinForward,
                Source = FeedUrl,
                InstalledAt = DateTimeOffset.UtcNow,
                Contributions = contributions,
            };
            store.Save(registry);

            return new
            {
                PackageId = packageId,
                Version = version,
                Verb = primaryVerb,
                Verbs = verbs,
                Pinned = Pin || carryPinForward,
                InstalledExtensionPath = installPath,
                Contributions = contributions.Select(c => new { c.Verb, c.Path }).ToArray(),
            };
        }
        catch
        {
            // Roll back the file-system side of the install. The registry was either not touched
            // or its old contents are unchanged (Save throws before partial writes are visible).
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
    /// Phase 10 #41: validate that an `extension add` for <paramref name="packageId"/> is allowed
    /// to overwrite the existing registry entry. Throws <see cref="CirrusErrorException"/> with
    /// <c>PinViolation</c> (exit 6) when the existing entry is pinned and neither
    /// <paramref name="force"/> nor <paramref name="pin"/> was supplied. Returns <c>true</c> when
    /// the caller should carry the pinned flag forward onto the new entry, <c>false</c> otherwise.
    /// </summary>
    public static bool CheckPinPrecondition(ExtensionRegistry registry, string packageId, string version, bool force, bool pin)
    {
        if (registry.Extensions.TryGetValue(packageId, out var existingEntry) && existingEntry.Pinned)
        {
            if (!force && !pin)
            {
                throw CirrusErrorException.PinViolation(
                    $"Extension '{packageId}' is pinned at version '{existingEntry.Version}'. " +
                    $"Pass --force to overwrite, or 'extension unpin {packageId}' first.",
                    new Dictionary<string, object?>
                    {
                        ["packageId"] = packageId,
                        ["pinnedVersion"] = existingEntry.Version,
                        ["requestedVersion"] = version,
                    });
            }
            return true;
        }
        return false;
    }

    private static (string PackageId, string Version) ParseInstallPath(string installRoot, string installPath)
    {
        var relative = System.IO.Path.GetRelativePath(installRoot, installPath);
        var parts = relative.Split(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        if (parts.Length < 2)
        {
            throw new InvalidOperationException(
                $"Installed path '{installPath}' does not match the expected '{{installRoot}}/{{packageId}}/{{version}}' layout.");
        }
        return (parts[0], parts[1]);
    }

    private static void PurgeSiblingVersions(string installRoot, string packageId, string keepVersion)
    {
        var packageDir = System.IO.Path.Combine(installRoot, packageId);
        if (!Directory.Exists(packageDir))
        {
            return;
        }

        foreach (var versionDir in Directory.GetDirectories(packageDir))
        {
            var versionName = System.IO.Path.GetFileName(versionDir);
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
        catch (IOException)
        {
            // Best-effort cleanup; surfacing here would mask the original error from the caller.
        }
        catch (UnauthorizedAccessException)
        {
            // Same — best-effort.
        }
    }
}