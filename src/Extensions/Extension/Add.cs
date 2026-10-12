// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Extensions;

namespace Microsoft.Azure.Azclips.Extensions.Extension.Processors;

public class AddExtensionProcessor : IProcessor
{
    private OutputStreamDelegate _writeOutput = null!;

    [ArgumentInfo(
    Required = false,
    Description = @"Package id (full or short suffix) of the extension to add from a feed. Verb and namespace tokens are not accepted. Omit when using --path.",
    SerializedName = @"Name")]
    public string? Name { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"Optional version to install when using --name. Omit to use the latest available package version.",
    SerializedName = @"Version")]
    public string? Version { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"NuGet feed URL to install from when using --name.",
    SerializedName = @"FeedUrl")]
    public string? FeedUrl { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"Path to a local extension .nupkg file to install.",
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
            throw AzclipsErrorException.UsageError("Either Name or Path must be provided to add an extension.");
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        var loggingService = AzclipsServiceProvider.GetRequiredService<ILoggingService>();
        using var activity = telemetryService.StartActivity($"{nameof(AddExtensionProcessor)}.{nameof(Process)}");

        // Tag the activity with extension dimensions early so failed installs (mid-iteration
        // throws) still carry whatever was determined before the throw. `extension.source`
        // distinguishes feed installs from local-path sideloads ("path" sentinel).
        activity?.SetTag("extension.source", FeedUrl ?? "path");

        var installer = new ExtensionInstaller(loggingService, feedUrl: FeedUrl);
        string installPath;
        if (!string.IsNullOrWhiteSpace(Name))
        {
            // Resolve --name through the hint table so a verb (e.g. "vm") works symmetrically
            // with the dispatcher's unknown-verb suggestion; full package ids passthrough.
            var resolvedPackageId = ResolvePackageId(Name!, ExtensionHintTable.Default);
            try
            {
                installPath = installer.InstallExtensionAsync(
                    resolvedPackageId,
                    Version,
                    includePrerelease: true,
                    includeUnlisted: false,
                    CancellationToken.None
                ).GetAwaiter().GetResult();
            }
            catch (InvalidInputException ex) when (ex.Message.StartsWith("Could not find package", StringComparison.Ordinal))
            {
                // Feed miss: normalize to typed ExtensionNotFound and append a local-artifact
                // suggestion if a fresh nupkg exists in artifacts/Debug/.
                var localHint = LocalArtifactProbe.Find(resolvedPackageId);
                var details = new Dictionary<string, object?>
                {
                    ["packageId"] = resolvedPackageId,
                    ["requestedName"] = Name,
                };
                if (localHint is not null)
                {
                    details["localArtifact"] = localHint;
                    throw AzclipsErrorException.ExtensionNotFound(
                        $"{ex.Message}\n\nA local build artifact was found. Try:\n  az extension add --path \"{localHint}\"",
                        details);
                }
                throw AzclipsErrorException.ExtensionNotFound(ex.Message, details);
            }
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
            // Compatibility check before any expensive validation. Reads
            // [AzclipsCompatibility] via MetadataLoadContext (no exec); failure rolls back below.
            // TODO: replace literal "1.0.0" with a single host-version source-of-truth.
            var compatResult = CompatibilityGuard.CheckAssembly(installPath, currentCoreVersion: "1.0.0");
            if (!compatResult.IsCompatible)
            {
                throw AzclipsErrorException.CompatibilityViolation(
                    $"Extension '{packageId}' is not compatible with this host: {compatResult.Reason}.",
                    new Dictionary<string, object?>
                    {
                        ["packageId"] = packageId,
                        ["version"] = version,
                        ["reason"] = compatResult.Reason,
                    });
            }

            // Read declared verb(s) from installed DLLs without executing extension code.
            // [ExtensionVerb] is repeatable; an extension may claim multiple namespaces.
            var verbs = ExtensionVerbReader.ReadVerbs(installPath);
            if (verbs.Count == 0)
            {
                throw AzclipsErrorException.VerbConflict(
                    $"Extension '{packageId}' does not declare an [ExtensionVerb] attribute. " +
                    $"AzClips extensions must declare at least one top-level verb.",
                    new Dictionary<string, object?> { ["packageId"] = packageId });
            }
            activity?.SetTag("extension.namespace_count", verbs.Count);

            // First declared verb is the v1 `verb` field; legacy readers see something sensible.
            var primaryVerb = verbs[0];

            var store = AzclipsServiceProvider.GetRequiredService<IExtensionRegistryStore>();
            var registry = store.Load();

            // Verbs are shared across extensions; collisions resolve per-leaf at the (verb, path)
            // tuple level below via ExtensionAddPlanner's LeafConflict guard.

            // Read leaves (s_cliTable keys) from the installed DLLs and conflict-check every
            // (verb, path) tuple against the registry.
            var leafKeys = LeafReader.ReadCliLeafKeys(installPath);
            activity?.SetTag("extension.contribution_count", leafKeys.Count);
            // File registry only carries preferences (contributions are stripped on Save); overlay
            // live discovery so PlanContributions can see already-installed leaves and detect collisions.
            var planningRegistry = DiscoveryBackedExtensionRegistry.BuildDiscoveredWithPreferences(registry);
            var contributions = new List<Contribution>(
                ExtensionAddPlanner.PlanContributions(packageId, verbs, leafKeys, planningRegistry));

            // Per Q6 (strict single-version): purge any sibling version directories under the
            // package's parent dir so only the just-installed version remains on disk.
            PurgeSiblingVersions(installer.InstallPath, packageId, keepVersion: version);

            // Honor pins forward across in-place upgrades (PinViolation unless --force);
            // carry the pin onto the new entry when --force is used.
            bool carryPinForward = CheckPinPrecondition(registry, packageId, version, Force, Pin);

            registry.Extensions[packageId] = new ExtensionRegistryEntry
            {
                Version = version,
                Pinned = Pin || carryPinForward,
                Source = FeedUrl,
                InstalledAt = DateTimeOffset.UtcNow,
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
            // Roll back the install dir; registry is unchanged (Save throws before partial writes).
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
    /// Validate that an `extension add` for <paramref name="packageId"/> is allowed to overwrite
    /// the existing registry entry. Throws <see cref="AzclipsErrorException"/> with
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
                throw AzclipsErrorException.PinViolation(
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

    /// <summary>
    /// Resolve the user-supplied <c>--name</c> token to a NuGet package id. Verbs are not valid
    /// install identifiers — a single package can contribute many verbs (Compute owns both
    /// <c>vm</c> and <c>disk</c>), so accepting a verb would silently install the whole package.
    /// Symmetric with the remove/show/update/pin/unpin commands.
    /// <list type="number">
    ///   <item>Dotted token → passthrough (e.g. <c>Contoso.Foo</c>, <c>Microsoft.Azure.Azclips.Extensions.Compute</c>).</item>
    ///   <item>Bare token matching a verb in <see cref="ExtensionHintTable"/> → throw
    ///         <see cref="AzclipsErrorException.UsageError(string, IReadOnlyDictionary{string, object?}?)"/>
    ///         pointing at the owning package id.</item>
    ///   <item>Otherwise → legacy <c>Microsoft.Azure.Azclips.Extensions.{name}</c> prefix-strip
    ///         (first-party short names like <c>Resources</c>, <c>Compute</c>).</item>
    /// </list>
    /// </summary>
    public static string ResolvePackageId(string name, ExtensionHintTable hints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(hints);

        // (1) Dotted token => already a package id. Don't second-guess the user; this also lets
        // third-party extensions (Contoso.Foo) install via --name without a hint entry.
        if (name.Contains('.'))
        {
            return name;
        }

        // (2) Bare token matches a verb in the hint catalog: reject with a precise suggestion.
        // Verbs are sub-units of a package, not packages — installing by verb would silently
        // pull in unrelated sibling verbs (e.g. `add --name disk` would install Compute, which
        // also ships `vm`). The user should name the package directly.
        if (hints.TryGetPackageId(name, out var owningPackage))
        {
            var suggestion = RegistryLookup.ToShortName(owningPackage);
            throw AzclipsErrorException.UsageError(
                $"'{name}' is a verb contributed by package '{owningPackage}', not a package identifier. " +
                $"To install that package use:\n  az extension add --name {suggestion}",
                new Dictionary<string, object?>
                {
                    ["requestedName"] = name,
                    ["owningPackageId"] = owningPackage,
                });
        }

        // (3) First-party prefix-strip fallback.
        return $"Microsoft.Azure.Azclips.Extensions.{name}";
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
