using System.Reflection;
using Core.Abstractions;

namespace Core.Engine;

/// <summary>
/// Binds parsed command line input to ICommand implementations from dynamically loaded modules.
/// </summary>
internal class CommandBinder
{
    public ICommand? Bind(ParseResult parseResult)
    {
        if (parseResult.Commands.Count() == 0)
        {
            return null;
        }

        // naive implementation of dynamic module loading:
        // 1. use the first command as the module/extension name
        // 2. load the module dynamically
        // 3. get the commands from the module
        var moduleName = parseResult.Commands.First();
        var module = LoadModule(moduleName);
        var commands = module.GetCommands();
        // For simplicity, return the first command found
        // In a real implementation, you would match the command name to the appropriate command
        return commands.FirstOrDefault();
    }

    private IModule LoadModule(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            throw new ArgumentException("Extension name cannot be empty.", nameof(moduleName));
        }

        // Modules can be in two locations:
        // 1. Same directory as Core.Engine.dll: /bin/Group/Group.dll (published layout)
        // 2. Sibling to parent directory: /artifacts/Release/Group/Group.dll (build artifacts layout)
        var baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
        
        // Try to find module directory case-insensitively in current directory first
        var moduleDir = Directory.GetDirectories(baseDirectory)
            .FirstOrDefault(d => string.Equals(Path.GetFileName(d), moduleName, StringComparison.OrdinalIgnoreCase));
        
        // If not found in current directory, try parent's sibling directories
        if (moduleDir == null)
        {
            var parentDirectory = Directory.GetParent(baseDirectory)?.FullName;
            if (parentDirectory != null)
            {
                moduleDir = Directory.GetDirectories(parentDirectory)
                    .FirstOrDefault(d => string.Equals(Path.GetFileName(d), moduleName, StringComparison.OrdinalIgnoreCase));
            }
        }
        
        if (moduleDir == null)
        {
            throw new DirectoryNotFoundException($"Module directory not found for extension '{moduleName}' in '{baseDirectory}' or its parent's siblings.");
        }
        
        var moduleDirName = Path.GetFileName(moduleDir);
        var modulePath = Path.Combine(moduleDir, $"{moduleDirName}.dll");

        if (!File.Exists(modulePath))
        {
            throw new FileNotFoundException($"Module assembly not found for extension '{moduleName}'.", modulePath);
        }

        var moduleAssembly = Assembly.LoadFrom(modulePath);
        // Q: What if it has dependencies? Do we load them too?
        // Should we load into separate AssemblyLoadContext?
        // Should we load all assemblies in that folder?
        foreach (var type in moduleAssembly.GetTypes())
        {
            if (typeof(IModule).IsAssignableFrom(type) && !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) is not null)
            {
                return (IModule)Activator.CreateInstance(type)!;
            }
        }

        throw new InvalidOperationException($"No IModule implementation found in '{modulePath}'.");
    }
}


