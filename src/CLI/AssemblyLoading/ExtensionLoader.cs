// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Runtime.Loader;
using Core.Abstractions;
using Core.Configuration.Options;
using Core.Extensions;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Core.Cli;

public interface IExtensionLoader
{
    Assembly? LoadExtension(string extensionName, string? version = null);

    /// <summary>
    /// Enumerates all extensions this loader knows about on disk. Returns
    /// <c>(folderName, version, location)</c> triples where <c>folderName</c> can be passed
    /// verbatim back to <see cref="LoadExtension"/> and <c>location</c> is the absolute path
    /// to the primary DLL that would be loaded (or <c>null</c> if this loader cannot report
    /// one). Default returns empty — loaders that cannot enumerate (e.g. built-in loaders
    /// without a static manifest) simply do not contribute.
    /// </summary>
    IEnumerable<(string Name, string Version, string? Location)> GetInstalledExtensions() => [];

    /// <summary>Enumerates owner-bound installations where supported, without loading DLLs.</summary>
    IEnumerable<InstalledExtensionRef> GetInstallations()
        => GetInstalledExtensions().Select(info => new InstalledExtensionRef(info.Name, info.Version, info.Location));
}

/// <summary>
/// Loads <b>Bundled</b> extensions — DLLs that ship alongside <c>az.exe</c> from the packaged install
/// (<c>&lt;install&gt;/Extensions/&lt;PackageId&gt;/</c>) or the local build output (<c>artifacts/&lt;Config&gt;/</c>).
/// </summary>
/// <remarks>
/// <para>
/// The runtime knows only two tiers: <b>Bundled</b> (this loader) and <b>Installed</b>
/// (<c>InstalledExtensionLoader</c>, which covers extensions placed by <c>az extension add</c>
/// under <c>~/.azclips/extensions/</c>). Dispatch is uniform across both — no Core-tier exception.
/// The set of extensions expected to ship in every install lives in the packaging profile
/// (<c>devops/scripts/packaging/config/lean_package.config</c>), not the runtime.
/// </para>
/// <para>
/// <b>Version resolution</b> for a Bundled extension is driven by the on-disk <c>.nupkg</c>
/// (produced by <c>GeneratePackageOnBuild=true</c> in local builds). Packaged installs strip the
/// <c>.nupkg</c> at packaging time, so <see cref="GetInstalledExtensions"/> reports
/// <c>Version = string.Empty</c> for those — dispatch then takes the unversioned load path.
/// See <c>docs/spec/extension-framework.md §3.2</c>.
/// </para>
/// </remarks>
public class BundledExtensionLoader(ILoggingService loggingService, IConfigurationService configurationService) : IExtensionLoader
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<(ExtensionLoadContext Ctx, Assembly Assembly)>> s_cache =
        new(StringComparer.Ordinal);

    internal class ExtensionLoadContext(string extensionPath, ILoggingService? loggingService = null) : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver = new(extensionPath);
        private readonly ILoggingService? _loggingService = loggingService;

        internal Assembly LoadManagedAssembly(string assemblyPath)
        {
            using var stream = File.OpenRead(assemblyPath);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            return LoadFromStream(ms);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name != null && (assemblyName.Name.StartsWith("Core.") || assemblyName.Name.StartsWith("Microsoft.Extensions.DependencyInjection")))
            {
                _loggingService?.Debug($"[{nameof(BundledExtensionLoader)}] Skipping shared assembly '{assemblyName.Name}'.");
                return null;
            }
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                _loggingService?.Debug($"[{nameof(BundledExtensionLoader)}] Loading dependency assembly from '{assemblyPath}'.");
                return LoadManagedAssembly(assemblyPath);
            }
            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }
            return IntPtr.Zero;
        }
    }

    private readonly ILoggingService? _loggingService = loggingService;
    private readonly AssemblyLoadingOptions _options = configurationService.GetConfiguration<AssemblyLoadingOptions>();
    private readonly string _ownerId = Guid.NewGuid().ToString("N");

    private static IEnumerable<string> GetCandidateDirectoryNames(string extensionName, string configuredPrefix)
    {
        var names = Constants.extensionPackagePrefixes
            .Select(prefix => string.Concat(prefix, extensionName))
            .Prepend(string.Concat(configuredPrefix ?? string.Empty, extensionName))
            .Append(extensionName);

        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetCandidateAssemblyNames(string extensionName)
    {
        var names = Constants.extensionPackagePrefixes
            .Select(prefix => string.Concat(prefix, extensionName))
            .Prepend(extensionName);

        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsKnownExtensionPackageId(string packageId, string configuredPrefix)
    {
        var prefixes = Constants.extensionPackagePrefixes
            .Prepend(configuredPrefix ?? string.Empty)
            .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return prefixes.Any(prefix =>
            packageId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Default root directory for bundled extensions: the packaging layout
    /// (<c>&lt;install&gt;/Extensions/</c>) in Release, or <c>artifacts/&lt;Config&gt;/</c> in
    /// local dev builds. Not user-configurable — packaging always writes here.
    /// </summary>
    public static string GetDefaultRoot()
    {
#if DEBUG
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
#else
        return Path.Combine(AppContext.BaseDirectory, "Extensions");
#endif
    }

    private static IEnumerable<string> GetCandidateRoots()
    {
        var roots = new[]
        {
            GetDefaultRoot(),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "Debug")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "Release"))
        };

        return roots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Attempts to load a Bundled extension by name, optionally at a specific version.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Unversioned request</b> (<paramref name="version"/> null or empty): returns the first
    /// on-disk match under any configured Bundled root. Used by the <c>az login</c>-style path where
    /// dispatch has no version to enforce.
    /// </para>
    /// <para>
    /// <b>Versioned request:</b> only serves the load when the on-disk <c>.nupkg</c> in the package
    /// folder reports the exact same version. If the folder has no <c>.nupkg</c> (packaged install)
    /// or the versions mismatch, returns <c>null</c> so the loader chain can defer to
    /// <c>InstalledExtensionLoader</c>. This preserves user-installed shadowing: a user who
    /// <c>az extension add</c>'s a newer version wins over the Bundled copy.
    /// </para>
    /// <para>
    /// The versioned-match branch was added to fix Azure/CLIPS#522: local Debug/Release builds
    /// discover Bundled extensions with a real version from the <c>.nupkg</c>, and dispatch then
    /// requires that version — previously refused, causing "could not be found by any extension
    /// loader" errors even though the DLL was sitting right there.
    /// </para>
    /// </remarks>
    public Assembly? LoadExtension(string extensionName, string? version = null)
    {
        _loggingService?.Debug($"[{nameof(BundledExtensionLoader)}] Resolving extension '{extensionName}' (version='{version ?? "any"}').");
        _loggingService?.Debug($"[{nameof(BundledExtensionLoader)}] Searching root(s): '{string.Join(";", GetCandidateRoots().Where(Directory.Exists))}'.");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        foreach (var root in GetCandidateRoots())
        {
            foreach (var directoryName in GetCandidateDirectoryNames(extensionName, _options.ExtensionFolderPrefix))
            {
                var packageDir = Path.Combine(root, directoryName);
                if (!Directory.Exists(packageDir))
                {
                    continue;
                }

                // Versioned request: only serve if the on-disk .nupkg matches exactly. Unversioned
                // request: trust the caller and take the first assembly-name match. See Azure/CLIPS#522.
                if (!string.IsNullOrEmpty(version))
                {
                    var discoveredVersion = ResolvePackageVersion(packageDir, directoryName, _loggingService);
                    if (string.IsNullOrEmpty(discoveredVersion) ||
                        !string.Equals(discoveredVersion, version, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                foreach (var assemblyName in GetCandidateAssemblyNames(extensionName))
                {
                    var extensionPath = Path.Combine(packageDir, $"{assemblyName}.dll");
                    if (!File.Exists(extensionPath))
                    {
                        continue;
                    }

                    var loadedVersion = string.IsNullOrEmpty(version)
                        ? ResolvePackageVersion(packageDir, directoryName, _loggingService)
                        : version;
                    return LoadFromPath(extensionName, loadedVersion, extensionPath, stopwatch: stopwatch);
                }
            }
        }

        _loggingService?.Debug($"[{nameof(BundledExtensionLoader)}] Extension '{extensionName}' (version='{version ?? "any"}') not found; will try other loaders. Checked root(s): '{string.Join(";", GetCandidateRoots())}'.");
        return null;
    }

    /// <inheritdoc/>
    public IEnumerable<(string Name, string Version, string? Location)> GetInstalledExtensions()
        => GetInstallations().Select(info => (info.Name, info.Version, info.Location));

    public IEnumerable<InstalledExtensionRef> GetInstallations()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (root, rootPriority) in GetCandidateRoots().Select((root, priority) => (root, priority)))
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var packageDir in Directory.GetDirectories(root))
            {
                var packageId = Path.GetFileName(packageDir);
                if (string.IsNullOrWhiteSpace(packageId))
                {
                    continue;
                }

                if (!IsKnownExtensionPackageId(packageId, _options.ExtensionFolderPrefix))
                {
                    continue;
                }

                var shortName = Constants.StripKnownExtensionPrefix(packageId);
                if (seen.Contains(shortName))
                {
                    continue;
                }

                var assemblyPath = GetCandidateAssemblyNames(shortName)
                    .Select(assemblyName => Path.Combine(packageDir, $"{assemblyName}.dll"))
                    .FirstOrDefault(File.Exists);

                if (assemblyPath is null)
                {
                    continue;
                }

                var discoveredVersion = ResolvePackageVersion(packageDir, packageId, _loggingService);

                seen.Add(shortName);
                var snapshot = new ExtensionFileSnapshot(shortName, discoveredVersion, assemblyPath, _ownerId);
                yield return new InstalledExtensionRef(shortName, discoveredVersion, snapshot.Location)
                {
                    Installation = new ExtensionInstallation(
                        shortName, discoveredVersion, snapshot.Location, "Bundled", 0, rootPriority,
                        snapshot.CacheKey,
                        () => LoadFromPath(shortName, discoveredVersion, snapshot.Location, snapshot),
                        snapshot.ValidateUnchanged),
                };
            }
        }
    }

    private Assembly LoadFromPath(string name, string version, string extensionPath, ExtensionFileSnapshot? snapshot = null,
        System.Diagnostics.Stopwatch? stopwatch = null)
    {
        stopwatch ??= System.Diagnostics.Stopwatch.StartNew();
        snapshot?.ValidateUnchanged();
        (ExtensionLoadContext, Assembly) Load()
        {
            snapshot?.ValidateUnchanged();
            var ctx = new ExtensionLoadContext(extensionPath, _loggingService);
            var assembly = ctx.LoadManagedAssembly(extensionPath);
            snapshot?.ValidateUnchanged();
            return (ctx, assembly);
        }

        bool isCached = false;
        Assembly result;
        if (_options.BypassExtensionCache)
        {
            result = Load().Item2;
        }
        else
        {
            var candidate = new Lazy<(ExtensionLoadContext, Assembly)>(Load);
            var key = snapshot?.CacheKey ?? "legacy:" + ExtensionFileSnapshot.CanonicalizePath(extensionPath);
            var entry = s_cache.GetOrAdd(key, candidate);
            isCached = !ReferenceEquals(entry, candidate);
            result = entry.Value.Assembly;
        }
        snapshot?.ValidateUnchanged();
        stopwatch.Stop();
        _loggingService?.Debug($"[{nameof(BundledExtensionLoader)}] Loaded '{name}' v{(string.IsNullOrEmpty(version) ? "?" : version)} from '{extensionPath}' in {stopwatch.Elapsed.TotalMilliseconds:F1}ms.");
        ExtensionLoadTracker.Record(name, isCached ? "Cached" : "Bundled", stopwatch.Elapsed.TotalMilliseconds, version, extensionPath);
        return result;
    }

    private static string ResolvePackageVersion(string packageDir, string packageId, ILoggingService? loggingService)
    {
        var prefix = packageId + ".";
        var versions = Directory.EnumerateFiles(packageDir, "*.nupkg", SearchOption.TopDirectoryOnly)
            .Select(nupkgPath => ParseVersionFromNupkg(nupkgPath, packageId, prefix, loggingService))
            .Where(parsed => parsed is not null)
            .Cast<NuGetVersion>()
            .OrderByDescending(parsed => parsed)
            .ToList();

        if (versions.Count > 0)
        {
            loggingService?.Debug($"[{nameof(BundledExtensionLoader)}] Resolved version '{versions[0]}' for '{packageId}' via .nupkg scan.");
            return versions[0].ToString();
        }

        // Fallback when no .nupkg is present in the package directory.
        var attributeVersion = AssemblyVersionReader.TryReadInformationalVersion(packageDir, packageId);
        if (!string.IsNullOrEmpty(attributeVersion))
        {
            loggingService?.Debug($"[{nameof(BundledExtensionLoader)}] Resolved version '{attributeVersion}' for '{packageId}' via AssemblyInformationalVersion (no .nupkg present).");
            return attributeVersion;
        }

        return string.Empty;
    }

    private static NuGetVersion? ParseVersionFromNupkg(string nupkgPath, string packageId, string packagePrefix, ILoggingService? loggingService)
    {
        var identityVersion = TryReadPackageIdentityVersion(nupkgPath, packageId);
        if (identityVersion is not null)
        {
            return identityVersion;
        }

        loggingService?.Debug($"Failed to read package metadata from '{nupkgPath}', falling back to filename parsing.");

        var fileName = Path.GetFileName(nupkgPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return ParseVersionFromNupkgFileName(fileName, packagePrefix);
    }

    private static NuGetVersion? TryReadPackageIdentityVersion(string nupkgPath, string packageId)
    {
        try
        {
            using var stream = File.OpenRead(nupkgPath);
            using var reader = new PackageArchiveReader(stream);
            PackageIdentity identity = reader.GetIdentity();

            if (!string.Equals(identity.Id, packageId, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return identity.Version;
        }
        catch
        {
            return null;
        }
    }

    private static NuGetVersion? ParseVersionFromNupkgFileName(string fileName, string packagePrefix)
    {
        if (!fileName.StartsWith(packagePrefix, StringComparison.OrdinalIgnoreCase) ||
            !fileName.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var versionText = fileName[packagePrefix.Length..^".nupkg".Length];
        return NuGetVersion.TryParse(versionText, out var parsed) ? parsed : null;
    }
}

public class InstalledExtensionLoader(ILoggingService loggingService, IConfigurationService configurationService) : IExtensionLoader
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<(ExtensionLoadContext Ctx, Assembly Assembly)>> s_cache =
        new(StringComparer.Ordinal);

    internal class ExtensionLoadContext(string extensionDirectory, ILoggingService? loggingService = null) : AssemblyLoadContext
    {
        public string ExtensionDirectory { get; } = extensionDirectory;
        private readonly ILoggingService? _loggingService = loggingService;

        internal Assembly LoadManagedAssembly(string assemblyPath)
        {
            using var stream = File.OpenRead(assemblyPath);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            return LoadFromStream(ms);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name != null && (assemblyName.Name.StartsWith("Core.") || assemblyName.Name.StartsWith("Microsoft.Extensions.DependencyInjection")))
            {
                _loggingService?.Debug($"[{nameof(InstalledExtensionLoader)}] Skipping shared assembly '{assemblyName.Name}'.");
                return null;
            }
            string assemblyPath = Path.Combine(ExtensionDirectory, $"{assemblyName.Name}.dll");
            if (File.Exists(assemblyPath))
            {
                return LoadManagedAssembly(assemblyPath);
            }
            return null;
        }
    }

    private readonly ILoggingService? _loggingService = loggingService;
    private readonly AssemblyLoadingOptions _options = configurationService.GetConfiguration<AssemblyLoadingOptions>();
    private readonly string _ownerId = Guid.NewGuid().ToString("N");

    private static IEnumerable<string> GetCandidateDirectoryNames(string extensionName, string configuredPrefix)
    {
        var names = Constants.extensionPackagePrefixes
            .Select(prefix => string.Concat(prefix, extensionName))
            .Prepend(string.Concat(configuredPrefix ?? string.Empty, extensionName))
            .Append(extensionName);

        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetCandidateAssemblyNames(string extensionName)
    {
        var names = Constants.extensionPackagePrefixes
            .Select(prefix => string.Concat(prefix, extensionName))
            .Prepend(extensionName);

        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private string? ResolveExtensionDirectory(string extensionName, string? version)
    {
        foreach (var candidateName in GetCandidateDirectoryNames(extensionName, _options.ExtensionFolderPrefix))
        {
            var candidateRoot = Path.Combine(_options.InstalledExtensionsRoot, candidateName);
            if (!Directory.Exists(candidateRoot))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(version))
            {
                var exactVersionDirectory = Path.Combine(candidateRoot, version);
                if (Directory.Exists(exactVersionDirectory))
                {
                    return exactVersionDirectory;
                }

                continue;
            }

            var versionDirs = Directory.GetDirectories(candidateRoot);
            if (versionDirs.Length == 0)
            {
                continue;
            }

            var latestDir = versionDirs
                .Select(dir => (dir, parsed: NuGetVersion.TryParse(Path.GetFileName(dir), out var parsedVersion) ? parsedVersion : null))
                .Where(item => item.parsed != null)
                .OrderByDescending(item => item.parsed)
                .Select(item => item.dir)
                .FirstOrDefault();

            if (latestDir is not null)
            {
                return latestDir;
            }
        }

        return null;
    }

    internal InstalledExtensionRef? ResolveInstallation(string extensionName, string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var extensionDir = ResolveExtensionDirectory(extensionName, version);
        if (string.IsNullOrWhiteSpace(extensionDir))
        {
            return null;
        }

        var path = GetCandidateAssemblyNames(extensionName)
            .Select(assemblyName => Path.Combine(extensionDir, $"{assemblyName}.dll"))
            .FirstOrDefault(File.Exists);
        return path is null ? null : CreateInstallation(extensionName, version, path);
    }

    public Assembly? LoadExtension(string extensionName, string? version = null)
    {
        _loggingService?.Debug($"[{nameof(InstalledExtensionLoader)}] Resolving extension '{extensionName}' (version='{version ?? "any"}').");
        _loggingService?.Debug($"[{nameof(InstalledExtensionLoader)}] Searching root: '{_options.InstalledExtensionsRoot}'.");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var extensionDir = ResolveExtensionDirectory(extensionName, version);
        if (extensionDir is null)
        {
            _loggingService?.Debug($"[{nameof(InstalledExtensionLoader)}] Extension '{extensionName}' not found under installed extensions root. Root: '{_options.InstalledExtensionsRoot}'.");
            return null;
        }

        var extensionPath = GetCandidateAssemblyNames(extensionName)
            .Select(assemblyName => Path.Combine(extensionDir, $"{assemblyName}.dll"))
            .FirstOrDefault(File.Exists);

        if (extensionPath is null)
        {
            _loggingService?.Debug($"[{nameof(InstalledExtensionLoader)}] Extension '{extensionName}' version '{version}' has no matching .dll under '{extensionDir}'.");
            return null;
        }

        var resolvedVersion = version ?? Path.GetFileName(extensionDir);
        return LoadFromPath(extensionName, resolvedVersion, extensionPath, stopwatch: stopwatch);
    }

    private Assembly LoadFromPath(string name, string version, string extensionPath, ExtensionFileSnapshot? snapshot = null,
        System.Diagnostics.Stopwatch? stopwatch = null)
    {
        stopwatch ??= System.Diagnostics.Stopwatch.StartNew();
        snapshot?.ValidateUnchanged();
        (ExtensionLoadContext, Assembly) Load()
        {
            snapshot?.ValidateUnchanged();
            var ctx = new ExtensionLoadContext(Path.GetDirectoryName(Path.GetFullPath(extensionPath))!, _loggingService);
            var assembly = ctx.LoadManagedAssembly(extensionPath);
            snapshot?.ValidateUnchanged();
            return (ctx, assembly);
        }

        bool isCached = false;
        Assembly assembly;
        if (_options.BypassExtensionCache)
        {
            assembly = Load().Item2;
        }
        else
        {
            var candidate = new Lazy<(ExtensionLoadContext, Assembly)>(Load);
            // On a race, GetOrAdd returns the winner's entry, so reference equality shows whether this call created it.
            var key = snapshot?.CacheKey ?? "legacy:" + ExtensionFileSnapshot.CanonicalizePath(extensionPath);
            var entry = s_cache.GetOrAdd(key, candidate);
            isCached = !ReferenceEquals(entry, candidate);
            assembly = entry.Value.Assembly;
        }

        snapshot?.ValidateUnchanged();
        stopwatch.Stop();
        var loaderName = isCached ? "Cached" : "Installed";
        _loggingService?.Debug($"[{nameof(InstalledExtensionLoader)}] Loaded '{name}' v{version} from '{extensionPath}' in {stopwatch.Elapsed.TotalMilliseconds:F1}ms.");
        ExtensionLoadTracker.Record(name, loaderName, stopwatch.Elapsed.TotalMilliseconds, version, extensionPath);
        return assembly;
    }

    private InstalledExtensionRef CreateInstallation(string name, string version, string path)
    {
        var snapshot = new ExtensionFileSnapshot(name, version, path, _ownerId);
        return new InstalledExtensionRef(name, version, snapshot.Location)
        {
            Installation = new ExtensionInstallation(
                name, version, snapshot.Location, "Installed", sourcePriority: 1, rootPriority: 0, snapshot.CacheKey,
                () => LoadFromPath(name, version, snapshot.Location, snapshot), snapshot.ValidateUnchanged),
        };
    }

    /// <inheritdoc/>
    public IEnumerable<(string Name, string Version, string? Location)> GetInstalledExtensions()
        => GetInstallations().Select(info => (info.Name, info.Version, info.Location));

    public IEnumerable<InstalledExtensionRef> GetInstallations()
    {
        if (!Directory.Exists(_options.InstalledExtensionsRoot))
        {
            yield break;
        }

        var packageDirs = Directory.GetDirectories(_options.InstalledExtensionsRoot);

        foreach (var packageDir in packageDirs)
        {
            var fullPackageName = Path.GetFileName(packageDir);
            // Strip any known prefix to get the short extension name.
            var shortName = Constants.StripKnownExtensionPrefix(fullPackageName);

            // Pins are resolved to a concrete installation by CLIAssemblyLoadingProvider.
            var latestVersion = Directory.GetDirectories(packageDir)
                .Select(versionDir =>
                {
                    var versionName = Path.GetFileName(versionDir);
                    var parsed = NuGetVersion.TryParse(versionName, out var parsedVersion)
                        ? parsedVersion
                        : null;
                    return (versionName, parsedVersion: parsed);
                })
                .Where(item => item.parsedVersion is not null)
                .OrderByDescending(item => item.parsedVersion)
                .Select(item => item.versionName)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(latestVersion))
            {
                continue;
            }

            var latestVersionDir = Path.Combine(packageDir, latestVersion);
            var assemblyPath = GetCandidateAssemblyNames(shortName)
                .Select(assemblyName => Path.Combine(latestVersionDir, $"{assemblyName}.dll"))
                .FirstOrDefault(File.Exists);

            if (assemblyPath is null)
            {
                // Version directory exists but the DLL is missing (corrupt/partial install).
                // Skip — dispatch couldn't load it anyway, and consumers rely on Name/Version pairs
                // being backed by a loadable assembly. Matches BundledExtensionLoader's gate.
                continue;
            }

            // Normalize to an absolute path — Location is documented as absolute, but a
            // user-configured relative InstalledExtensionsRoot would otherwise leak through.
            yield return CreateInstallation(shortName, latestVersion, assemblyPath);
        }
    }
}
