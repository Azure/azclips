using System.Reflection;
using System.Runtime.Loader;
using Core.Abstractions;
using Core.Cli.Exceptions;

namespace Core.Cli.Parser;

public class ExtensionDispatcher //: IParser
{
    // Flag-style help tokens that may appear anywhere after the subcommand. The literal
    // `help` is intentionally NOT here so a positional value of "help" (e.g. a config
    // key/value) is not misinterpreted as a help request. The subcommand form
    // (`azv2 help <group> ...`) is handled explicitly via args[0] below.
    private static readonly string[] s_helpFlags = ["--help", "-h", "-?", "?"];

    private readonly record struct HelpRequest(string ExtensionName, IReadOnlyList<string> Path, bool IsCommandHelp);

    internal class ExtensionLoadContext(string extensionDirectory) : AssemblyLoadContext
    {
        public string ExtensionDirectory { get; } = extensionDirectory;

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == "SharedContracts" || assemblyName.Name == "Core.SharedContracts")
            {
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

    public ExtensionDispatcher()
    {
    }

    public CommandInvocation Parse(string[] args)
    {
        if (args.Length == 0) throw new ArgumentException("Arguments cannot be empty.", nameof(args));

        // Bare top-level help: any of the flag forms (--help, -h, -?, ?) OR the literal
        // `help` subcommand with no further tokens. We only honor literal `help` here
        // (args.Length == 1) and via the explicit subcommand form below; we deliberately
        // do NOT treat a trailing `help` token elsewhere in the command line as a help
        // request, since "help" is a valid positional value for some commands.
        if (args.Length == 1 && (IsHelpToken(args[0]) || args[0].Equals("help", StringComparison.OrdinalIgnoreCase)))
        {
            throw new DispatchHelpException(this);
        }

        if (TryCreateHelpRequest(args, out var helpRequest))
        {
            throw new RuntimeExtensionHelpException(helpRequest.ExtensionName, helpRequest.Path, helpRequest.IsCommandHelp);
        }

        string extensionName = CommandGroupIndex.getExtensionNameByCommandGroupName($"{char.ToUpperInvariant(args[0][0])}{args[0][1..]}");
        // string dllName = $"Microsoft.Azure.Cirrus.Extensions.{extensionName}";
        // string dllPath = Path.Combine(ExtensionDirectory, dllName, $"{dllName}.dll");
        // if (!File.Exists(dllPath)) throw new FileNotFoundException($"Extension assembly not found: {dllPath}");

        var extensionParser = new ExtensionParser();
        (string commandName, IDictionary<string, object> parameters) = extensionParser.Parse(args[1..]);
        return new CommandInvocation(extensionName, commandName, parameters, IsTransient: true, ModuleName: extensionName);
    }

    private static bool IsHelpToken(string token)
        => s_helpFlags.Contains(token, StringComparer.OrdinalIgnoreCase);

    private static bool TryCreateHelpRequest(string[] args, out HelpRequest request)
    {
        request = default;

        string[] targetTokens;
        if (args[0].Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            targetTokens = args[1..].Where(static token => !IsHelpToken(token)).ToArray();
        }
        else if (args.Skip(1).Any(IsHelpToken))
        {
            targetTokens = args.Where(static token => !IsHelpToken(token)).ToArray();
        }
        else
        {
            return false;
        }

        if (targetTokens.Length == 0)
        {
            return false;
        }

        var groupToken = targetTokens[0];
        var extensionName = CommandGroupIndex.getExtensionNameByCommandGroupName($"{char.ToUpperInvariant(groupToken[0])}{groupToken[1..]}");

        var commandSegments = targetTokens
            .Skip(1)
            .TakeWhile(static token => !token.StartsWith('-'))
            .Select(static token => token.ToLowerInvariant())
            .ToArray();

        var path = new List<string> { groupToken.ToLowerInvariant() };
        path.AddRange(commandSegments);

        request = new HelpRequest(extensionName, path, commandSegments.Length > 0);
        return true;
    }
}
