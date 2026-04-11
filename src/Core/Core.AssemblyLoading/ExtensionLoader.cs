using System.Reflection;
using System.Runtime.Loader;
using Core.Abstractions;
using NuGet.Versioning;

namespace Core.AssemblyLoading;

public interface IExtensionLoader
{
    public Assembly? LoadExtension(string extensionName, string? version = null);
}

public class BuiltinExtensionLoader(ILoggingService loggingService) : IExtensionLoader
{
    private static readonly string installationDir = Constants.builtinInstallationDir;

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

    public Assembly? LoadExtension(string extensionName, string? version = null)
    {
        string extensionNameFull = Constants.cirrusPrefix + extensionName;
        if (!string.IsNullOrEmpty(version))
        {
            return null;
        }
        var extensionPath = Path.Combine(installationDir, Constants.extensionFolderPrefix + extensionName, $"{extensionNameFull}.dll");
        if (!File.Exists(extensionPath))
        {
            _loggingService?.Debug($"Extension '{extensionNameFull}' not found in builtin extensions directory, will try other sources. Checked path: '{extensionPath}'");
            return null;
        }
        var loadContext = new ExtensionLoadContext(extensionPath, _loggingService);
        return loadContext.LoadFromAssemblyPath(extensionPath);
    }
}

public class MultiVersionExtensionLoader(ILoggingService loggingService) : IExtensionLoader
{
    private static readonly string installationDir = Constants.versionedInstallationDir;

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

    public Assembly? LoadExtension(string extensionName, string? version = null)
    {
        string extensionNameFull = Constants.cirrusPrefix + extensionName;
        string extensionDir;
        if (!string.IsNullOrEmpty(version))
        {
            _loggingService?.Debug($"Loading extension '{extensionName}' version '{version}' from multi-version extensions directory.");
            extensionDir = Path.Combine(installationDir, Constants.extensionFolderPrefix + extensionName, version);
        }
        else
        {
            if (!Directory.Exists(Path.Combine(installationDir, Constants.extensionFolderPrefix + extensionName)))
            {
                _loggingService?.Debug($"Extension '{extensionName}' not found in multi-version extensions directory, will try other sources. Checked path: '{Path.Combine(installationDir, Constants.extensionFolderPrefix + extensionName)}'");
                return null;
            }
            // Load the latest version
            var versionDirs = Directory.GetDirectories(Path.Combine(installationDir, Constants.extensionFolderPrefix + extensionName));
            if (versionDirs.Length == 0)
            {
                _loggingService?.Debug($"No versions found for extension '{extensionName}' in multi-version extensions directory. Checked path: '{Path.Combine(installationDir, Constants.extensionFolderPrefix + extensionName)}'");
                return null;
            }
            // Use NuGet.Versioning for proper semantic version comparison, skipping invalid version names
            string? latestDir = versionDirs
                .Select(dir => (dir, parsed: NuGetVersion.TryParse(Path.GetFileName(dir), out var v) ? v : null))
                .Where(x => x.parsed != null)
                .OrderByDescending(x => x.parsed)
                .Select(x => x.dir)
                .FirstOrDefault();
            if (latestDir == null)
            {
                _loggingService?.Debug($"No valid semantic versions found for extension '{extensionName}'. Checked path: '{Path.Combine(installationDir, Constants.extensionFolderPrefix + extensionName)}'");
                return null;
            }
            extensionDir = latestDir;
        }

        var extensionPath = Path.Combine(extensionDir, $"{extensionNameFull}.dll");
        if (!File.Exists(extensionPath))
        {
            _loggingService?.Debug($"Extension '{extensionNameFull}' version '{version}' not found in multi-version extensions directory. Checked path: '{extensionPath}'");
            return null;
        }
        var loadContext = new ExtensionLoadContext(extensionPath, _loggingService);
        return loadContext.LoadFromAssemblyPath(extensionPath);
    }
}