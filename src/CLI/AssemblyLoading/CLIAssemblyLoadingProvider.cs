namespace Core.Cli;

using System.Reflection;
using Core.Abstractions;

public class CLIAssemblyLoadingProvider(IEnumerable<IExtensionLoader> extensionLoaders, ILoggingService loggingService) : IAssemblyLoadingProvider
{
    private Assembly? LoadExtension(IEnumerable<IExtensionLoader> extensionLoaders, string extensionName, string? version = null) => extensionLoaders
                    .Select(loader => loader.LoadExtension(extensionName, version))
                    .FirstOrDefault(assembly => assembly != null);

    public Assembly LoadExtension(string extensionName, string? extensionVersion = null)
    {
        loggingService.Info($"Processing command with extension '{Constants.cirrusPrefix + extensionName}'.");
        return LoadExtension(extensionLoaders, extensionName, extensionVersion) ?? throw new InvalidOperationException($"Extension '{Constants.cirrusPrefix + extensionName}' could not be found by any extension loader.");
    }

    public IList<InstalledExtensionRef>? GetAllInstalledExtensions()
    {
        Console.Error.WriteLine("[TRACE] CLIAssemblyLoadingProvider.GetAllInstalledExtensions START");
        var logPath = Path.Combine(Path.GetTempPath(), "cirrus-discovery.log");
        File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] CLIAssemblyLoadingProvider.GetAllInstalledExtensions called with {extensionLoaders.Count()} loaders\n");
        
        var result = new List<InstalledExtensionRef>();
        foreach (var loader in extensionLoaders)
        {
            var loaderType = loader.GetType().Name;
            Console.Error.WriteLine($"[TRACE] Querying loader: {loaderType}");
            File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] Querying loader: {loaderType}\n");
            
            Console.Error.WriteLine($"[TRACE] About to call loader.GetInstalledExtensions()");
            try
            {
                Console.Error.WriteLine($"[TRACE] Before GetInstalledExtensions()");
                var installed = loader.GetInstalledExtensions();
                Console.Error.WriteLine($"[TRACE] After GetInstalledExtensions(), got IEnumerable, about to Count()");
                
                var count = 0;
                try
                {
                    Console.Error.WriteLine($"[TRACE] About to enumerate with Count()");
                    count = installed.Count();
                    Console.Error.WriteLine($"[TRACE] Count() returned: {count}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[TRACE] ERROR enumerating {loaderType}: {ex.GetType().Name}: {ex.Message}");
                    File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] ERROR enumerating {loaderType}: {ex.GetType().Name}: {ex.Message}\n");
                    File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] Stack: {ex.StackTrace}\n");
                    continue;
                }
                
                Console.Error.WriteLine($"[TRACE] Loader {loaderType} returned {count} extensions");
                File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] Loader {loaderType} returned {count} extensions\n");
                
                foreach (var (name, version) in installed)
                {
                    Console.Error.WriteLine($"[TRACE]   - {name} v{version}");
                    File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}]   - {name} v{version}\n");
                    result.Add(new InstalledExtensionRef(name, version));
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[TRACE] ERROR in GetAllInstalledExtensions from {loaderType}: {ex.GetType().Name}: {ex.Message}");
                File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] ERROR in GetAllInstalledExtensions from {loaderType}: {ex.GetType().Name}: {ex.Message}\n");
                File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] Stack: {ex.StackTrace}\n");
            }
        }
        
        Console.Error.WriteLine($"[TRACE] CLIAssemblyLoadingProvider.GetAllInstalledExtensions returning {result.Count} total");
        File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] CLIAssemblyLoadingProvider.GetAllInstalledExtensions returning {result.Count} total\n");
        return result.Count > 0 ? result : null;
    }
}