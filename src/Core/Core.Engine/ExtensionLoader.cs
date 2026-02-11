using System.Reflection;
using System.Runtime.Loader;
using Core.Abstractions;

namespace Core.Engine;

public interface IExtensionLoader
{
    public Assembly? LoadExtension(string extensionFolderName, string extensionName, string? version = null);
}

public class BuiltinExtensionLoader(string extensionsDir, ILoggingService? loggingService = null) : IExtensionLoader
{
    internal class ExtensionLoadContext(string extensionPath, ILoggingService? loggingService = null) : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver = new(extensionPath);
        private readonly ILoggingService? _loggingService = loggingService;

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name != null && assemblyName.Name.StartsWith("Core."))
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

    public Assembly? LoadExtension(string extensionFolderName, string extensionName, string? version = null)
    {
        if (version != null)
        {
            return null;
        }
        var extensionPath = Path.Combine(extensionsDir, extensionFolderName, $"{extensionName}.dll");
        if (!File.Exists(extensionPath))
        {
            _loggingService?.Debug($"Extension '{extensionName}' not found in builtin extensions directory, will try other sources. Checked path: '{extensionPath}'");
            return null;
        }
        var loadContext = new ExtensionLoadContext(extensionPath, _loggingService);
        return loadContext.LoadFromAssemblyPath(extensionPath);
    }
}

public class MultiVersionExtensionLoader(string extensionsDir, ILoggingService? loggingService = null) : IExtensionLoader
{
    internal class ExtensionLoadContext(string extensionDirectory, ILoggingService? loggingService = null) : AssemblyLoadContext
    {
        public string ExtensionDirectory { get; } = extensionDirectory;
        private readonly ILoggingService? _loggingService = loggingService;

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == "SharedContracts" || assemblyName.Name == "Core.SharedContracts")
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

    public Assembly? LoadExtension(string extensionFolderName, string extensionName, string? version = null)
    {
        string extensionDir;
        if (version != null)
        {
            _loggingService?.Debug($"Loading extension '{extensionName}' version '{version}' from multi-version extensions directory.");
            extensionDir = Path.Combine(extensionsDir, extensionName, version);
        }
        else
        {
            if (!Directory.Exists(Path.Combine(extensionsDir, extensionName)))
            {
                _loggingService?.Debug($"Extension '{extensionName}' not found in multi-version extensions directory, will try other sources. Checked path: '{Path.Combine(extensionsDir, extensionName)}'");
                return null;
            }
            // Load the latest version
            var versionDirs = Directory.GetDirectories(Path.Combine(extensionsDir, extensionName));
            if (versionDirs.Length == 0)
            {
                _loggingService?.Debug($"No versions found for extension '{extensionName}' in multi-version extensions directory. Checked path: '{Path.Combine(extensionsDir, extensionName)}'");
                return null;
            }
            // TODO: Improve version comparison to handle semantic versioning properly
            Array.Sort(versionDirs);
            extensionDir = versionDirs[^1];
        }

        var extensionPath = Path.Combine(extensionDir, $"{extensionName}.dll");
        if (!File.Exists(extensionPath))
        {
            _loggingService?.Debug($"Extension '{extensionName}' version '{version}' not found in multi-version extensions directory. Checked path: '{extensionPath}'");
            return null;
        }
        var loadContext = new ExtensionLoadContext(extensionPath, _loggingService);
        return loadContext.LoadFromAssemblyPath(extensionPath);
    }
}