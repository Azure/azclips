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
}

/// <summary>
/// Loads <b>Bundled</b> extensions — DLLs that ship alongside <c>az.exe</c> from the packaged install
/// (<c>&lt;install&gt;/Extensions/&lt;PackageId&gt;/</c>) or the local build output (<c>artifacts/&lt;Config&gt;/</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Terminology note (pending rename in a follow-up PR):</b> the name <c>Builtin*</c> is legacy.
/// This loader serves the <b>Bundled</b> tier, which is distinct from the <b>Core</b> tier
/// (the compile-time seed in <see cref="Core.Extensions.BuiltInExtensions.Map"/>).
/// Core and Bundled are orthogonal: packaging profiles (lean vs full, see
/// <c>inject_builtin_extensions.sh</c>) can ship any subset of Core extensions, and Bundled-only
/// extensions like <c>Resources</c> and <c>Compute</c> aren't seeded in
/// <see cref="Core.Extensions.BuiltInExtensions.Map"/> at all. The <b>Installed</b> tier
/// (<c>MultiVersionExtensionLoader</c>) covers extensions placed by <c>az extension add</c> under
/// <c>~/.azclips/extensions/</c>.
/// </para>
/// <para>
/// <b>Version resolution</b> for a Bundled extension is driven by the on-disk <c>.nupkg</c>
/// (produced by <c>GeneratePackageOnBuild=true</c> in local builds). Packaged installs strip the
/// <c>.nupkg</c> at packaging time, so <see cref="GetInstalledExtensions"/> reports
/// <c>Version = string.Empty</c> for those — dispatch then takes the unversioned load path.
/// See <c>docs/spec/extension-framework.md §3.2</c>.
/// </para>
/// </remarks>
public class BuiltinExtensionLoader(ILoggingService loggingService, IConfigurationService configurationService) : IExtensionLoader
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<(ExtensionLoadContext Ctx, Assembly Assembly)>> s_cache =
        new(StringComparer.OrdinalIgnoreCase);

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
                _loggingService?.Debug($"[{nameof(BuiltinExtensionLoader)}] Skipping shared assembly '{assemblyName.Name}'.");
                return null;
            }
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                _loggingService?.Debug($"[{nameof(BuiltinExtensionLoader)}] Loading dependency assembly from '{assemblyPath}'.");
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

    private static IEnumerable<string> GetCandidateRoots(string configuredBuiltinInstallationDir)
    {
        var roots = new[]
        {
            configuredBuiltinInstallationDir,
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
    /// <c>MultiVersionExtensionLoader</c>. This preserves user-installed shadowing: a user who
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
        _loggingService?.Debug($"[{nameof(BuiltinExtensionLoader)}] Resolving extension '{extensionName}' (version='{version ?? "any"}').");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        foreach (var root in GetCandidateRoots(_options.BuiltinInstallationDir))
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
                    var discoveredVersion = ResolvePackageVersionFromNupkg(packageDir, directoryName, _loggingService);
                    if (string.IsNullOrEmpty(discoveredVersion) ||
                        !string.Equals(discoveredVersion, version, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                foreach (var assemblyName in GetCandidateAssemblyNames(extensionName))
                {
                    var extensionPath = Path.Combine(packageDir, $"{assemblyName}.dll");
                    _loggingService?.Debug($"[{nameof(BuiltinExtensionLoader)}] Probing '{extensionPath}'.");
                    if (!File.Exists(extensionPath))
                    {
                        continue;
                    }

                    Assembly assembly;
                    if (_options.BypassExtensionCache)
                    {
                        var freshCtx = new ExtensionLoadContext(extensionPath, _loggingService);
                        assembly = freshCtx.LoadManagedAssembly(extensionPath);
                    }
                    else
                    {
                        var entry = s_cache.GetOrAdd(
                            extensionPath,
                            key => new Lazy<(ExtensionLoadContext, Assembly)>(() =>
                            {
                                var ctx = new ExtensionLoadContext(key, _loggingService);
                                return (ctx, ctx.LoadManagedAssembly(key));
                            }));
                        assembly = entry.Value.Assembly;
                    }
                    stopwatch.Stop();

                    var loadedVersion = string.IsNullOrEmpty(version)
                        ? ResolvePackageVersionFromNupkg(packageDir, directoryName, _loggingService)
                        : version;
                    _loggingService?.Debug($"[{nameof(BuiltinExtensionLoader)}] Loaded '{extensionName}' v{(string.IsNullOrEmpty(loadedVersion) ? "?" : loadedVersion)} from '{extensionPath}' in {stopwatch.Elapsed.TotalMilliseconds:F1}ms.");
                    ExtensionLoadTracker.Record(extensionName, stopwatch.Elapsed.TotalMilliseconds, loadedVersion, extensionPath);
                    return assembly;
                }
            }
        }

        _loggingService?.Debug($"[{nameof(BuiltinExtensionLoader)}] Extension '{extensionName}' (version='{version ?? "any"}') not found; will try other loaders. Checked root(s): '{string.Join(";", GetCandidateRoots(_options.BuiltinInstallationDir))}'.");
        return null;
    }

    /// <inheritdoc/>
    public IEnumerable<(string Name, string Version, string? Location)> GetInstalledExtensions()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in GetCandidateRoots(_options.BuiltinInstallationDir))
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

                var discoveredVersion = ResolvePackageVersionFromNupkg(packageDir, packageId, _loggingService);

                seen.Add(shortName);
                // Normalize to an absolute path — Location is documented as absolute, but a
                // user-configured relative BuiltinInstallationDir would otherwise leak through.
                yield return (shortName, discoveredVersion, Path.GetFullPath(assemblyPath));
            }
        }
    }

    private static string ResolvePackageVersionFromNupkg(string packageDir, string packageId, ILoggingService? loggingService)
    {
        var prefix = packageId + ".";
        var versions = Directory.EnumerateFiles(packageDir, "*.nupkg", SearchOption.TopDirectoryOnly)
            .Select(nupkgPath => ParseVersionFromNupkg(nupkgPath, packageId, prefix, loggingService))
            .Where(parsed => parsed is not null)
            .Cast<NuGetVersion>()
            .OrderByDescending(parsed => parsed)
            .ToList();

        return versions.Count == 0 ? string.Empty : versions[0].ToString();
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

public class MultiVersionExtensionLoader(ILoggingService loggingService, IConfigurationService configurationService) : IExtensionLoader
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string Directory, string AssemblyPath), Lazy<(ExtensionLoadContext Ctx, Assembly Assembly)>> s_cache =
        new();

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
                _loggingService?.Debug($"[{nameof(MultiVersionExtensionLoader)}] Skipping shared assembly '{assemblyName.Name}'.");
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
            var candidateRoot = Path.Combine(_options.VersionedInstallationDir, candidateName);
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

    internal bool HasInstalledVersion(string extensionName, string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var extensionDir = ResolveExtensionDirectory(extensionName, version);
        if (string.IsNullOrWhiteSpace(extensionDir))
        {
            return false;
        }

        return GetCandidateAssemblyNames(extensionName)
            .Select(assemblyName => Path.Combine(extensionDir, $"{assemblyName}.dll"))
            .Any(File.Exists);
    }

    public Assembly? LoadExtension(string extensionName, string? version = null)
    {
        _loggingService?.Debug($"[{nameof(MultiVersionExtensionLoader)}] Resolving extension '{extensionName}' (version='{version ?? "any"}').");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var extensionDir = ResolveExtensionDirectory(extensionName, version);
        if (extensionDir is null)
        {
            _loggingService?.Debug($"[{nameof(MultiVersionExtensionLoader)}] Extension '{extensionName}' not found under versioned installation directory. Root: '{_options.VersionedInstallationDir}'.");
            return null;
        }

        var extensionPath = GetCandidateAssemblyNames(extensionName)
            .Select(assemblyName => Path.Combine(extensionDir, $"{assemblyName}.dll"))
            .FirstOrDefault(File.Exists);

        if (extensionPath is null)
        {
            _loggingService?.Debug($"[{nameof(MultiVersionExtensionLoader)}] Extension '{extensionName}' version '{version}' has no matching .dll under '{extensionDir}'.");
            return null;
        }

        _loggingService?.Debug($"[{nameof(MultiVersionExtensionLoader)}] Probing '{extensionPath}'.");

        Assembly assembly;
        if (_options.BypassExtensionCache)
        {
            // ExtensionLoadContext expects a directory (Path.Combine in Load resolves siblings).
            // Cache hits below already pass key.Directory directly.
            var freshCtx = new ExtensionLoadContext(extensionDir, _loggingService);
            assembly = freshCtx.LoadManagedAssembly(extensionPath);
        }
        else
        {
            var entry = s_cache.GetOrAdd(
                (extensionDir, extensionPath),
                key => new Lazy<(ExtensionLoadContext, Assembly)>(() =>
                {
                    var ctx = new ExtensionLoadContext(key.Directory, _loggingService);
                    var asm = ctx.LoadManagedAssembly(key.AssemblyPath);
                    return (ctx, asm);
                }));
            assembly = entry.Value.Assembly;
        }

        stopwatch.Stop();
        var resolvedVersion = version ?? Path.GetFileName(extensionDir);
        _loggingService?.Debug($"[{nameof(MultiVersionExtensionLoader)}] Loaded '{extensionName}' v{resolvedVersion} from '{extensionPath}' in {stopwatch.Elapsed.TotalMilliseconds:F1}ms.");
        ExtensionLoadTracker.Record(extensionName, stopwatch.Elapsed.TotalMilliseconds, resolvedVersion, extensionPath);
        return assembly;
    }

    /// <inheritdoc/>
    public IEnumerable<(string Name, string Version, string? Location)> GetInstalledExtensions()
    {
        if (!Directory.Exists(_options.VersionedInstallationDir))
        {
            yield break;
        }

        var packageDirs = Directory.GetDirectories(_options.VersionedInstallationDir);

        foreach (var packageDir in packageDirs)
        {
            var fullPackageName = Path.GetFileName(packageDir);
            // Strip any known prefix to get the short extension name.
            var shortName = Constants.StripKnownExtensionPrefix(fullPackageName);

            // TODO(dlanguiller): In a future pass, allow this provider to honor persisted
            // pin/active-version preferences when selecting the resolved installed version.
            // For now, emit a single deterministic version per package: highest semver.
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
                // being backed by a loadable assembly. Matches BuiltinExtensionLoader's gate.
                continue;
            }

            // Normalize to an absolute path — Location is documented as absolute, but a
            // user-configured relative VersionedInstallationDir would otherwise leak through.
            yield return (shortName, latestVersion, Path.GetFullPath(assemblyPath));
        }
    }
}
