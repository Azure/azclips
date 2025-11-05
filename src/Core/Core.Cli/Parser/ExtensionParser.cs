using System.Reflection;
using Core.Cli.Exceptions;
using Core.SharedContracts;

namespace Core.Cli.Parser;

/// <summary>
/// Initializes a new instance of the <see cref="ExtensionParser"/> class.
/// </summary>
/// <param name="extensionAssembly">The assembly of the module.</param>
/// <exception cref="ArgumentNullException">Thrown when moduleAssembly is null.</exception>
/// <exception cref="ArgumentException">Thrown when moduleAssembly does not have a valid name.</exception>
public class ExtensionParser : IParser
{
    public Assembly ExtensionAssembly { get; }
    public ExtensionInfoAttribute ExtensionInfo { get; }
    public string AssemblyName { get; }
    public string ExtensionName { get; }

    public ExtensionParser(Assembly extensionAssembly)
    {
        ExtensionAssembly = extensionAssembly ?? throw new ArgumentNullException(nameof(extensionAssembly), "Module assembly cannot be null.");
        AssemblyName = extensionAssembly.GetName().Name ?? throw new ArgumentException("Module assembly must have a valid name.", nameof(extensionAssembly));
        ExtensionInfo = extensionAssembly.GetCustomAttribute<ExtensionInfoAttribute>()
            ?? throw new ArgumentException("Module assembly must have an ExtensionInfoAttribute.", nameof(extensionAssembly));
        ExtensionName = ExtensionInfo.Name;
    }

    public Task<ICommand<TResult>> ParseAsync<TResult>(string[] args)
    {
        var (command, commandArgs) = ParseCommand(args);
        return command.ParseAsync<TResult>(commandArgs);
    }

    public IEnumerable<CommandParser> GetCommands()
    {
        foreach (var type in ExtensionAssembly.GetTypes())
        {
            if (type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)) && !type.IsAbstract && !type.IsInterface)
            {
                CommandInfoAttribute? commandInfo = type.GetCustomAttribute<CommandInfoAttribute>();
                if (commandInfo != null)
                {
                    yield return new CommandParser(type);
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Type {type.Name} does not have CommandInfoAttribute, skipping");
                }
            }
        }
    }

    public IEnumerable<CommandParser> GetCommands(string commandGroup)
    {
        foreach (var command in GetCommands())
        {
            if (string.IsNullOrEmpty(commandGroup) || command.CommandInfo.Name.StartsWith(commandGroup + " ", StringComparison.OrdinalIgnoreCase))
            {
                yield return command;
            }
        }
    }

    (CommandParser, string[]) ParseCommand(string[] args)
    {
        string commandString = string.Join(" ", args);
        string sharedCommandGroup = string.Empty;
        foreach (var cmd in GetCommands())
        {
            var commandInfo = cmd.CommandInfo;
            // Assumes command names are unique and no command name is a prefix of another.
            // For example, "storage" and "storage account" would conflict in this implementation.
            if (commandString.StartsWith(commandInfo.Name + " ", StringComparison.OrdinalIgnoreCase))
            {
                // Extract arguments after the command name
                string[] commandArgs = [.. args.Skip(commandInfo.Name.Split(' ').Length)];
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Command found: {commandInfo.Name} with arguments: {string.Join(", ", commandArgs)}");
                return (cmd, commandArgs);
            }
            var commonPrefix = SharedCommandGroupPrefix(commandString, commandInfo.Name);
            if (commonPrefix.Length > sharedCommandGroup.Length)
            {
                sharedCommandGroup = commonPrefix;
            }
        }
        // Extract arguments after the command group name
        string[] groupArgs = string.IsNullOrWhiteSpace(sharedCommandGroup)
            ? [.. args]
            : [.. args.Skip(sharedCommandGroup.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length)];
        var helpFlags = new[] { "--help", "-h", "help", "?", "-?" };
        if (groupArgs.Length == 1 && helpFlags.Contains(groupArgs[0], StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Shared command group prefix found: {sharedCommandGroup}");
            throw new CommandGroupHelpException(this, sharedCommandGroup);
        }
        throw new CommandParserException($"Unrecognized command in Module {AssemblyName}: {commandString}");
    }

    static string SharedCommandGroupPrefix(string command, string commandName)
    {
        if (!commandName.EndsWith(' '))
            commandName += " ";

        var commonPrefix = new string([..command.Zip(commandName)
            .TakeWhile(pair => pair.First == pair.Second)
            .Select(pair => pair.First)]);
        
        var lastSpaceIndex = commonPrefix.LastIndexOf(' ');
        return lastSpaceIndex > 0 
            ? commonPrefix.Substring(0, lastSpaceIndex) 
            : string.Empty;
    }
}
