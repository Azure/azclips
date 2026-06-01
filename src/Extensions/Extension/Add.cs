using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Extensions;
using Core.Runtime;

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

        // Tag the activity with extension-shaped dimensions as soon as each value becomes
        // known so failed installs still carry whatever was determined before the throw
        // (e.g. a LeafConflict mid-iteration carries package_id/version/namespace_count and the
        // *attempted* contribution_count). OTEL semantic convention: dotted namespace, snake_case
        // attribute names. `extension.source` distinguishes feed installs from local-path side-
        // loads ("path" sentinel) for dev-loop vs production install ratio.
        activity?.SetTag("extension.source", FeedUrl ?? "path");

        var installer = new ExtensionInstaller(loggingService, feedUrl: FeedUrl);
        string installPath;
        if (!string.IsNullOrWhiteSpace(Name))
        {
            // Resolve --name through ResolvePackageId so a verb (e.g. "vm") works
            // symmetrically with the dispatcher's unknown-verb suggestion. Falls through to the
            // legacy prefix-strip when no hint matches; full package ids passthrough untouched.
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
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("Could not find package", StringComparison.Ordinal))
            {
                // Feed-miss path. Normalize the plain InvalidOperationException to a typed
                // ExtensionNotFound (exit 4) and append a local-artifact suggestion when a freshly
                // built nupkg exists in artifacts/Debug/. Suggestion-only — we never auto-install
                // from local, because that would silently consume stale builds.
                var localHint = LocalArtifactProbe.Find(resolvedPackageId);
                var details = new Dictionary<string, object?>
                {
                    ["packageId"] = resolvedPackageId,
                    ["requestedName"] = Name,
                };
                if (localHint is not null)
                {
                    details["localArtifact"] = localHint;
                    throw CirrusErrorException.ExtensionNotFound(
                        $"{ex.Message}\n\nA local build artifact was found. Try:\n  azv2 extension add --path \"{localHint}\"",
                        details);
                }
                throw CirrusErrorException.ExtensionNotFound(ex.Message, details);
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
            // Compatibility check before any expensive validation (spec §6). Reads
            // [CirrusCompatibility] from the installed DLLs via MetadataLoadContext (no exec),
            // and hard-fails the install if the declared range excludes the current core. The
            // catch handler below rolls back the install dir, so the on-disk state is clean.
            // The current core version is a literal here — a single host-version source-of-truth
            // is deferred (see vNext "host version" entry in the spec). Bump in lockstep with
            // any breaking change to the Core.Abstractions contract.
            var compatResult = CompatibilityGuard.CheckAssembly(installPath, currentCoreVersion: "1.0.0");
            if (!compatResult.IsCompatible)
            {
                throw CirrusErrorException.CompatibilityViolation(
                    $"Extension '{packageId}' is not compatible with this host: {compatResult.Reason}.",
                    new Dictionary<string, object?>
                    {
                        ["packageId"] = packageId,
                        ["version"] = version,
                        ["reason"] = compatResult.Reason,
                    });
            }

            // Per docs/spec/extension-management.md §5.2: read the declared verb(s) from the
            // installed DLLs *without* executing extension code, then validate against built-ins +
            // registry before committing the registry write. [ExtensionVerb] is repeatable, so an
            // extension may claim more than one namespace.
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

            // Top-level verbs are shared. Multiple extensions may declare the same
            // [ExtensionVerb] (e.g. two extensions both contributing under `vm`); collisions are
            // resolved per-leaf below against the (verb, path) tuple. Built-in verbs remain
            // reserved (see the IsBuiltInVerb check above) and built-in roots are still rejected
            // per-leaf.

            // Read the candidate's leaves (s_cliTable keys) from the just-installed DLLs and
            // conflict-check every (verb, path) tuple against the registry and the built-in
            // seed. This is finer-grained than the verb-only check above and catches the
            // cross-namespace case where two extensions share a verb but contribute different
            // paths (allowed) vs. the same path (rejected).
            var leafKeys = LeafReader.ReadCliLeafKeys(installPath);
            activity?.SetTag("extension.contribution_count", leafKeys.Count);
            var contributions = new List<Contribution>(
                ExtensionAddPlanner.PlanContributions(packageId, verbs, leafKeys, registry));

            // Per Q6 (strict single-version): purge any sibling version directories under the
            // package's parent dir so only the just-installed version remains on disk.
            PurgeSiblingVersions(installer.InstallPath, packageId, keepVersion: version);

            // 'add' must respect a pin set on the *existing* entry. Pinning's contract is
            // "don't change my version under me"; without this check, a pinned 1.1.0 could be
            // silently overwritten by `add --path …1.0.0…`. Honor the pin forward across the
            // in-place upgrade — fail with PinViolation unless --force, and carry the pin onto
            // the new entry when the user explicitly forces.
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
    /// Validate that an `extension add` for <paramref name="packageId"/> is allowed to overwrite
    /// the existing registry entry. Throws <see cref="CirrusErrorException"/> with
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

    /// <summary>
    /// Resolve the user-supplied <c>--name</c> token to a NuGet package id, in this order:
    /// <list type="number">
    ///   <item>If the token contains a dot, treat it as a fully-qualified package id and pass it
    ///         through unchanged (e.g. <c>Contoso.Foo</c>, <c>Microsoft.Azure.Cirrus.Extensions.Compute</c>).</item>
    ///   <item>If the token matches a verb in the embedded <see cref="ExtensionHintTable"/>, use
    ///         the package id that owns that verb. This is the symmetry path: the dispatcher's
    ///         unknown-verb message says "install with `azv2 extension add --name &lt;verb&gt;`",
    ///         so the install command must accept the verb directly.</item>
    ///   <item>Otherwise fall back to the legacy <c>Microsoft.Azure.Cirrus.Extensions.{name}</c>
    ///         prefix-strip convention for first-party short names (e.g. <c>Resources</c>,
    ///         <c>Compute</c>).</item>
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

        // (2) Hint catalog lookup as a bare verb. Case-insensitive (the table comparer is OIC).
        if (hints.TryGetPackageId(name, out var hinted))
        {
            return hinted;
        }

        // (3) First-party prefix-strip fallback.
        return $"Microsoft.Azure.Cirrus.Extensions.{name}";
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