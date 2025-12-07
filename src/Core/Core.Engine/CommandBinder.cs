using System.Reflection;
using Core.Abstractions;

namespace Core.Engine;

/// <summary>
/// Binds parsed command line input to ICommand implementations from dynamically loaded modules.
/// todo: consider breaking down into smaller classes/services
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

        // this assumes folder structure where modules are sibling folders to Core, e.g.
        // /Core
        // |-- Core.Engine.dll
        // /Group
        // |-- Group.dll
        var baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
        var modulePath = Path.GetFullPath(Path.Combine(baseDirectory, "..", moduleName, $"{moduleName}.dll"));

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


