using System.Reflection;
using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Engine;

namespace Core.Cli.Parser;

/// <summary>
/// Result of command resolution — contains the resolved Type and parsed parameters.
/// The engine will construct the IProcessor via ActivatorUtilities and bind parameters.
/// </summary>
public record ResolvedCommand(Type CommandType, IDictionary<string, object> Parameters);

/// <summary>
/// Resolves raw CLI arguments into a typed command and parsed parameters.
/// Performs: routing → extension loading → command matching → arg parsing → parameter binding.
/// The engine then constructs and executes the resolved command.
/// </summary>
public class CommandResolver
{
    private readonly List<IExtensionLoader> _extensionLoaders;
    private readonly ILoggingService? _loggingService;

    public CommandResolver(IEnumerable<IExtensionLoader> extensionLoaders, ILoggingService? loggingService = null)
    {
        _extensionLoaders = extensionLoaders.ToList();
        _loggingService = loggingService;
    }

    /// <summary>
    /// Resolves raw CLI args into a command Type and parsed parameters.
    /// The engine will construct the IProcessor via ActivatorUtilities and bind the parameters.
    /// </summary>
    public ResolvedCommand Resolve(string[] args)
    {
        // Step 1: Route to extension, get remaining args
        var router = new CommandRouter();
        var routed = router.Route(args);

        // Step 2: Load extension assembly and index BEFORE parsing
        var (extensionAssembly, extensionIndex) = LoadExtensionIndex(routed.ExtensionName);

        // Step 3: Match command name against known commands (longest match first)
        var (commandMetadata, remainingArgs) = MatchCommand(extensionIndex, routed.CommandArgs, routed.ExtensionName, routed.GroupName);
        _loggingService?.Info($"Resolved command type '{commandMetadata.CommandType.Name}' from extension '{routed.ExtensionName}'.");

        // Step 3.5: If remaining args contain a help flag, show command-level help
        if (ContainsHelpFlag(remainingArgs))
        {
            throw new CommandHelpException(routed.GroupName, routed.ExtensionName, commandMetadata);
        }

        // Step 4: Parse only the remaining args (after command name tokens consumed)
        var parsedArgs = ArgumentParser.Parse(remainingArgs);

        // Positional args are not supported — error if any are present
        if (parsedArgs.PositionalArgs.Count > 0)
        {
            throw new ArgumentException(
                $"Unexpected positional argument(s): {string.Join(", ", parsedArgs.PositionalArgs)}. "
                + "Positional arguments are not supported. Use named parameters (e.g., --name value).");
        }

        // Step 5: Parse CLI arguments into a typed parameter dictionary
        var parameters = ParseProperties(commandMetadata.CommandType, parsedArgs);

        return new ResolvedCommand(commandMetadata.CommandType, parameters);
    }

    private static readonly string[] HelpFlags = ["--help", "-h", "help", "?", "-?"];

    /// <summary>
    /// Returns true if any element in args is a recognized help flag.
    /// </summary>
    private static bool ContainsHelpFlag(string[] args)
        => args.Any(a => HelpFlags.Contains(a, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Matches command name tokens against the extension's known commands.
    /// Tries longest token sequence first, descending to shorter prefixes,
    /// and finally the empty-string default command.
    /// </summary>
    private (ICommandMetadata metadata, string[] remainingArgs) MatchCommand(
        IExtensionIndex extensionIndex, string[] commandArgs, string extensionName, string groupName)
    {
        // Collect leading tokens that are not flags (don't start with '-')
        int tokenCount = 0;
        for (int i = 0; i < commandArgs.Length; i++)
        {
            if (commandArgs[i].StartsWith('-')) break;
            tokenCount++;
        }

        // Try longest match first, descend to shorter
        for (int len = tokenCount; len >= 1; len--)
        {
            string candidateName = string.Join(" ", commandArgs[..len]);
            try
            {
                var metadata = extensionIndex.GetCommandMetaData(candidateName, ClientType.CLI);
                _loggingService?.Verbose($"Matched command '{candidateName}' in extension '{extensionName}'.");
                return (metadata, commandArgs[len..]);
            }
            catch (KeyNotFoundException)
            {
                // Not a match at this length, try shorter
            }
        }

        // Try empty-string default command
        try
        {
            var metadata = extensionIndex.GetCommandMetaData("", ClientType.CLI);
            _loggingService?.Verbose($"Matched default command in extension '{extensionName}'.");
            return (metadata, commandArgs);
        }
        catch (KeyNotFoundException)
        {
            // If no tokens and no default command, or help flag present, show group help
            if (commandArgs.Length == 0 || ContainsHelpFlag(commandArgs))
            {
                throw new CommandGroupHelpException(groupName, extensionName, extensionIndex);
            }

            string attempted = tokenCount > 0
                ? string.Join(" ", commandArgs[..tokenCount])
                : "(empty)";
            throw new InvalidOperationException(
                $"Command '{attempted}' not found in extension '{extensionName}'.");
        }
    }

    /// <summary>
    /// Parses CLI arguments into a typed parameter dictionary
    /// using ArgumentInfoAttribute metadata on the command type.
    /// </summary>
    private IDictionary<string, object> ParseProperties(Type commandType, ArgumentParser parsedArgs)
    {
        var parameters = new Dictionary<string, object>();

        foreach (var propertyInfo in commandType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            try
            {
                ArgumentInfoAttribute? attribute = propertyInfo.GetCustomAttribute<ArgumentInfoAttribute>();
                if (attribute != null)
                {
                    var value = parsedArgs.PopProperty(propertyInfo, attribute);
                    if (value != null)
                    {
                        _loggingService?.Verbose($"Parsed property '{propertyInfo.Name}' for command '{commandType.FullName}' to value '{value}'.");
                        parameters[propertyInfo.Name] = value;
                    }
                }
            }
            catch (PropertyParserException ex)
            {
                throw new ArgumentException(
                    $"Error parsing property '{propertyInfo.Name}' for command '{commandType.FullName}': {ex.Message}", ex);
            }
        }
        parsedArgs.ThrowRemainingProperties();
        return parameters;
    }

    /// <summary>
    /// Loads the extension assembly and creates its ExtensionIndex.
    /// Protected virtual to allow test overrides.
    /// </summary>
    protected virtual (Assembly assembly, IExtensionIndex index) LoadExtensionIndex(string extensionName)
    {
        var prefix = "Microsoft.Azure.Cirrus.Extensions.";
        var extensionFolderName = prefix + extensionName;
        var fullExtensionName = prefix + extensionName;

        Assembly extensionAssembly = _extensionLoaders
            .Select(loader => loader.LoadExtension(extensionFolderName, fullExtensionName))
            .FirstOrDefault(assembly => assembly != null)
            ?? throw new InvalidOperationException($"Extension '{extensionName}' could not be found by any extension loader.");
        _loggingService?.Verbose($"Loaded extension assembly '{extensionAssembly.FullName}'.");

        var typeName = $"{extensionAssembly.GetName().Name}.ExtensionIndex";
        Type indexType = extensionAssembly.GetType(typeName)
            ?? throw new InvalidOperationException($"Extension '{extensionName}' does not contain a {typeName} type.");

        IExtensionIndex extensionIndex;
        try
        {
            extensionIndex = Activator.CreateInstance(indexType) as IExtensionIndex
                ?? throw new InvalidOperationException($"Extension '{extensionName}' ExtensionIndex does not implement IExtensionIndex.");
        }
        catch (TargetInvocationException ex)
        {
            _loggingService?.Error($"Failed to create ExtensionIndex: {ex.InnerException?.Message}");
            throw new InvalidOperationException(
                $"Failed to instantiate ExtensionIndex for '{extensionName}': {ex.InnerException?.Message}",
                ex.InnerException);
        }

        return (extensionAssembly, extensionIndex);
    }
}
