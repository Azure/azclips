// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Runtime.Loader;
using Core.Abstractions;
using Core.Configuration.Options;
using Core.Extensions;
using NuGet.Versioning;

namespace Core.Cli;

public interface IExtensionLoader
{
    Assembly? LoadExtension(string extensionName, string? version = null);

    /// <summary>
    /// Enumerates all extensions this loader knows about on disk. Returns (folderName, version)
    /// pairs where <c>folderName</c> can be passed verbatim back to
    /// <see cref="LoadExtension"/>. Default returns empty — loaders that cannot enumerate
    /// (e.g. built-in loaders without a static manifest) simply do not contribute.
    /// </summary>
    IEnumerable<(string Name, string Version)> GetInstalledExtensions() => [];
}

public class BuiltinExtensionLoader(ILoggingService loggingService, IConfigurationService configurationService) : IExtensionLoader
{
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
                _loggingService?.Debug($"Skipping to load assembly '{assemblyName.Name}' in BuiltinExtensionLoader.");
                return null;
            }
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                _loggingService?.Debug($"Loading assembly from path: {assemblyPath}");
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

    public Assembly? LoadExtension(string extensionName, string? version = null)
    {
        if (!string.IsNullOrEmpty(version))
        {
            return null;
        }

        foreach (var root in GetCandidateRoots(_options.BuiltinInstallationDir))
        {
            foreach (var directoryName in GetCandidateDirectoryNames(extensionName, _options.ExtensionFolderPrefix))
            {
                foreach (var assemblyName in GetCandidateAssemblyNames(extensionName))
                {
                    var extensionPath = Path.Combine(root, directoryName, $"{assemblyName}.dll");
                    if (!File.Exists(extensionPath))
                    {
                        continue;
                    }

                    var loadContext = new ExtensionLoadContext(extensionPath, _loggingService);
                    return loadContext.LoadFromAssemblyPath(extensionPath);
                }
            }
        }

        _loggingService?.Debug($"Extension '{extensionName}' not found in builtin extensions directory, will try other sources. Checked root(s): '{string.Join(";", GetCandidateRoots(_options.BuiltinInstallationDir))}'");
        return null;
    }

    /// <inheritdoc/>
    public IEnumerable<(string Name, string Version)> GetInstalledExtensions()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in GetCandidateRoots(_options.BuiltinInstallationDir))
        {
            foreach (var builtIn in BuiltInExtensions.Map.Values)
            {
                var shortName = Constants.StripKnownExtensionPrefix(builtIn.PackageId);
                if (seen.Contains(shortName))
                {
                    continue;
                }

                foreach (var directoryName in GetCandidateDirectoryNames(shortName, _options.ExtensionFolderPrefix))
                {
                    var assemblyPath = Path.Combine(root, directoryName, $"{builtIn.PackageId}.dll");
                    if (!File.Exists(assemblyPath))
                    {
                        continue;
                    }

                    seen.Add(shortName);
                    yield return (shortName, string.Empty);
                    break;
                }
            }
        }
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
                _loggingService?.Debug($"Skipping to load assembly '{assemblyName.Name}' in MultiVersionExtensionLoader.");
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

    public Assembly? LoadExtension(string extensionName, string? version = null)
    {
        var extensionDir = ResolveExtensionDirectory(extensionName, version);
        if (extensionDir is null)
        {
            _loggingService?.Debug($"Extension '{extensionName}' not found in multi-version extensions directory, will try other sources. Checked root: '{_options.VersionedInstallationDir}'");
            return null;
        }

        var extensionPath = GetCandidateAssemblyNames(extensionName)
            .Select(assemblyName => Path.Combine(extensionDir, $"{assemblyName}.dll"))
            .FirstOrDefault(File.Exists);

        if (extensionPath is null)
        {
            _loggingService?.Debug($"Extension '{extensionName}' version '{version}' not found in multi-version extensions directory. Checked path: '{extensionDir}'");
            return null;
        }

        if (_options.BypassExtensionCache)
        {
            // ExtensionLoadContext expects a directory (Path.Combine in Load resolves siblings).
            // Cache hits below already pass key.Directory directly.
            var freshCtx = new ExtensionLoadContext(extensionDir, _loggingService);
            return freshCtx.LoadManagedAssembly(extensionPath);
        }

        var entry = s_cache.GetOrAdd(
            (extensionDir, extensionPath),
            key => new Lazy<(ExtensionLoadContext, Assembly)>(() =>
            {
                var ctx = new ExtensionLoadContext(key.Directory, _loggingService);
                var asm = ctx.LoadManagedAssembly(key.AssemblyPath);
                return (ctx, asm);
            }));
        return entry.Value.Assembly;
    }

    /// <inheritdoc/>
    public IEnumerable<(string Name, string Version)> GetInstalledExtensions()
    {
        if (!Directory.Exists(_options.VersionedInstallationDir))
        {
            yield break;
        }

        var packageDirs = Directory.GetDirectories(_options.VersionedInstallationDir);
        
        foreach (var packageDir in packageDirs)
        {
            var fullPackageName = Path.GetFileName(packageDir);
            // Strip the prefix to get the short name for the extension system.
            // Folder name: "Microsoft.Azure.Cirrus.Extensions.Resources"
            // Short name: "Resources"
            var shortName = fullPackageName.StartsWith(Constants.cirrusPrefix, StringComparison.OrdinalIgnoreCase)
                ? fullPackageName.Substring(Constants.cirrusPrefix.Length)
                : fullPackageName;
            
            var versionDirs = Directory.GetDirectories(packageDir);
            
            foreach (var versionDir in versionDirs)
            {
                var versionName = Path.GetFileName(versionDir);
                var isParsed = NuGetVersion.TryParse(versionName, out _);
                
                if (isParsed)
                {
                    yield return (shortName, versionName);
                }
            }
        }
    }
}