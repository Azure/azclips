using System.Reflection;
using Core.Abstractions;

namespace Core.Cli;

/// <summary>
/// Binds parsed commands to actual ICommand implementations by dynamically loading modules.
/// </summary>
public class CommandBinder
{
    private readonly ITelemetryService _telemetry;

    public CommandBinder(ITelemetryService telemetry)
    {
        _telemetry = telemetry;
    }

    /// <summary>
    /// Binds the parsed result to a command implementation.
    /// </summary>
    /// <param name="parseResult">The parsed command-line arguments</param>
    /// <returns>The command to execute, or null if not found</returns>
    public ICommand? Bind(ParseResult parseResult)
    {
        if (!parseResult.Commands.Any())
        {
            return null;
        }

        var moduleName = parseResult.Commands.First();
        var module = LoadModule(moduleName);

        if (module == null)
        {
            _telemetry.LogWarning($"Module '{moduleName}' not found.");
            return null;
        }

        var commands = module.GetCommands().ToList();

        if (!commands.Any())
        {
            _telemetry.LogWarning($"Module '{moduleName}' has no commands.");
            return null;
        }

        // If there's only one command word, return the first command
        if (parseResult.Commands.Count() == 1)
        {
            return commands.FirstOrDefault();
        }

        // Match subcommand to command type name (FIXED from v1 bug)
        var subcommand = parseResult.Commands.Skip(1).FirstOrDefault();
        if (string.IsNullOrEmpty(subcommand))
        {
            return commands.FirstOrDefault();
        }

        // Find command whose type name contains the subcommand
        var matchedCommand = commands.FirstOrDefault(c =>
            c.GetType().Name.Contains(subcommand, StringComparison.OrdinalIgnoreCase));

        if (matchedCommand == null)
        {
            _telemetry.LogWarning($"Command '{subcommand}' not found in module '{moduleName}'.");
        }

        return matchedCommand;
    }

    /// <summary>
    /// Dynamically loads a module by name from the modules directory.
    /// </summary>
    private IModule? LoadModule(string moduleName)
    {
        try
        {
            var baseDirectory = AppContext.BaseDirectory;
            var moduleDirectory = Path.Combine(baseDirectory, moduleName);

            // Case-insensitive directory search
            var parentDirectory = Path.GetDirectoryName(moduleDirectory);
            if (parentDirectory != null && Directory.Exists(parentDirectory))
            {
                var directories = Directory.GetDirectories(parentDirectory);
                var actualDirectory = directories.FirstOrDefault(d =>
                    Path.GetFileName(d).Equals(moduleName, StringComparison.OrdinalIgnoreCase));

                if (actualDirectory != null)
                {
                    moduleDirectory = actualDirectory;
                }
            }

            if (!Directory.Exists(moduleDirectory))
            {
                return null;
            }

            // Look for module DLL
            var moduleDll = Path.Combine(moduleDirectory, $"{moduleName}.dll");
            if (!File.Exists(moduleDll))
            {
                // Try case-insensitive file search
                var files = Directory.GetFiles(moduleDirectory, "*.dll");
                moduleDll = files.FirstOrDefault(f =>
                    Path.GetFileNameWithoutExtension(f).Equals(moduleName, StringComparison.OrdinalIgnoreCase)) ?? moduleDll;
            }

            if (!File.Exists(moduleDll))
            {
                return null;
            }

            // Load assembly and find IModule implementation
            var moduleAssembly = Assembly.LoadFrom(moduleDll);
            
            // GetTypes() may throw if some types can't be loaded (e.g., PowerShell cmdlets)
            // Use GetExportedTypes() or catch ReflectionTypeLoadException
            Type[] types;
            try
            {
                types = moduleAssembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Some types couldn't be loaded (e.g., PowerShell cmdlets when PS not available)
                // Use the types that did load
                types = ex.Types.Where(t => t != null).ToArray()!;
            }
            
            foreach (var type in types)
            {
                if (typeof(IModule).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                {
                    return (IModule)Activator.CreateInstance(type)!;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _telemetry.LogError($"Error loading module '{moduleName}'", ex);
            return null;
        }
    }
}
