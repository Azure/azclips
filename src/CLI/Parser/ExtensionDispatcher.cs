using System.Reflection;
using System.Runtime.Loader;
using Core.Abstractions;
using Core.Cli.Exceptions;

namespace Core.Cli.Parser;

public class ExtensionDispatcher //: IParser
{
    // Flag forms only. Literal `help` is excluded so it isn't mistaken for a positional
    // value; the `azv2 help <group>` subcommand form is matched explicitly via args[0].
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
        if (string.IsNullOrWhiteSpace(args[0])) throw new ArgumentException("Command-group token cannot be empty or whitespace.", nameof(args));

        // Bare top-level help: `azv2 --help`, `azv2 -h`, `azv2 help`, etc.
        if (IsBareTopLevelHelp(args))
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

    private static bool IsBareTopLevelHelp(string[] args)
        => args.Length == 1 && (IsHelpToken(args[0]) || IsHelpSubcommand(args[0]));

    private static bool IsHelpSubcommand(string token)
        => token.Equals("help", StringComparison.OrdinalIgnoreCase);

    private static bool IsHelpToken(string token)
        => s_helpFlags.Contains(token, StringComparer.OrdinalIgnoreCase);

    private static bool TryCreateHelpRequest(string[] args, out HelpRequest request)
    {
        request = default;

        // Two recognized shapes:
        //   1. `azv2 help <group> [...]`        — strip args[0]
        //   2. `azv2 <group> [...] --help [...]` — strip the help flag in place
        string[] targetTokens;
        if (IsHelpSubcommand(args[0]))
        {
            targetTokens = StripHelpFlags(args[1..]);
        }
        else if (args.Skip(1).Any(IsHelpToken))
        {
            targetTokens = StripHelpFlags(args);
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
        if (string.IsNullOrWhiteSpace(groupToken))
        {
            return false;
        }

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

    private static string[] StripHelpFlags(string[] tokens)
        => tokens.Where(static t => !IsHelpToken(t)).ToArray();
}
