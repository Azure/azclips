using System.Reflection;
using System.Runtime.Loader;
using Core.Abstractions;
using Core.Configuration.Options;
using NuGet.Versioning;

namespace Core.Cli;

public interface IExtensionLoader
{
    public Assembly? LoadExtension(string extensionName, string? version = null);
}

public class BuiltinExtensionLoader(ILoggingService loggingService, IConfigurationService configurationService) : IExtensionLoader
{
    internal class ExtensionLoadContext(string extensionPath, ILoggingService? loggingService = null) : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver = new(extensionPath);
        private readonly ILoggingService? _loggingService = loggingService;

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
                return LoadFromAssemblyPath(assemblyPath);
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
        var names = new[]
        {
            string.Concat(configuredPrefix ?? string.Empty, extensionName),
            string.Concat(Constants.cirrusPrefix, extensionName),
            extensionName
        };

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
        string extensionNameFull = Constants.cirrusPrefix + extensionName;
        if (!string.IsNullOrEmpty(version))
        {
            return null;
        }

        foreach (var root in GetCandidateRoots(_options.BuiltinInstallationDir))
        {
            foreach (var directoryName in GetCandidateDirectoryNames(extensionName, _options.ExtensionFolderPrefix))
            {
                var extensionPath = Path.Combine(root, directoryName, $"{extensionNameFull}.dll");
                if (!File.Exists(extensionPath))
                {
                    continue;
                }

                var loadContext = new ExtensionLoadContext(extensionPath, _loggingService);
                return loadContext.LoadFromAssemblyPath(extensionPath);
            }
        }

        _loggingService?.Debug($"Extension '{extensionNameFull}' not found in builtin extensions directory, will try other sources. Checked root(s): '{string.Join(";", GetCandidateRoots(_options.BuiltinInstallationDir))}'");
        return null;
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
                return LoadFromAssemblyPath(assemblyPath);
            }
            return null;
        }
    }

    private readonly ILoggingService? _loggingService = loggingService;
    private readonly AssemblyLoadingOptions _options = configurationService.GetConfiguration<AssemblyLoadingOptions>();

    private static IEnumerable<string> GetCandidateDirectoryNames(string extensionName, string configuredPrefix)
    {
        var names = new[]
        {
            string.Concat(configuredPrefix ?? string.Empty, extensionName),
            string.Concat(Constants.cirrusPrefix, extensionName),
            extensionName
        };

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
        string extensionNameFull = Constants.cirrusPrefix + extensionName;

        var extensionDir = ResolveExtensionDirectory(extensionName, version);
        if (extensionDir is null)
        {
            _loggingService?.Debug($"Extension '{extensionName}' not found in multi-version extensions directory, will try other sources. Checked root: '{_options.VersionedInstallationDir}'");
            return null;
        }

        var extensionPath = Path.Combine(extensionDir, $"{extensionNameFull}.dll");
        if (!File.Exists(extensionPath))
        {
            _loggingService?.Debug($"Extension '{extensionNameFull}' version '{version}' not found in multi-version extensions directory. Checked path: '{extensionPath}'");
            return null;
        }

        if (_options.BypassExtensionCache)
        {
            var freshCtx = new ExtensionLoadContext(extensionPath, _loggingService);
            return freshCtx.LoadFromAssemblyPath(extensionPath);
        }

        // Key by (resolved directory, dll path) so wildcard 'latest' resolutions and explicit
        // version pins share a slot once they pick the same folder.
        var entry = s_cache.GetOrAdd(
            (extensionDir, extensionPath),
            key => new Lazy<(ExtensionLoadContext, Assembly)>(() =>
            {
                var ctx = new ExtensionLoadContext(key.Directory, _loggingService);
                var asm = ctx.LoadFromAssemblyPath(key.AssemblyPath);
                return (ctx, asm);
            }));
        return entry.Value.Assembly;
    }
}