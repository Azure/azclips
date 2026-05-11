using System.Reflection;
using System.Runtime.Loader;
using Core.Abstractions;
using Core.Cli.Exceptions;

namespace Core.Cli.Parser;

public class ExtensionDispatcher //: IParser
{
    private static readonly string[] s_helpFlags = ["--help", "-h", "help", "?", "-?"];

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

        if (args.Length == 1 && IsHelpToken(args[0]))
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

    //public Task<ICommand<TResult>> ParseAsync<TResult>(string[] args)
    //{
    //    if (args.Length == 0) throw new ArgumentException("Arguments cannot be empty.", nameof(args));
    //    var helpFlags = new[] { "--help", "-h", "help", "?", "-?" };
    //    if (args.Length == 1 && helpFlags.Contains(args[0], StringComparer.OrdinalIgnoreCase)) throw new DispatchHelpException(this);

    //    string dllName = $"Extension.{char.ToUpperInvariant(args[0][0])}{args[0][1..]}";
    //    string dllPath = Path.Combine(ExtensionDirectory, $"{dllName}.dll");
    //    if (!File.Exists(dllPath)) throw new FileNotFoundException($"Extension assembly not found: {dllPath}");

    //    var loadContext = new ExtensionLoadContext(ExtensionDirectory);
    //    var assembly = loadContext.LoadFromAssemblyPath(dllPath);
    //    // var assembly = Assembly.LoadFrom(dllPath);
    //    var extensionParser = new ExtensionParser(assembly);
    //    return extensionParser.ParseAsync<TResult>(args[1..]);
    //}

    //public IEnumerable<ExtensionInfoAttribute> GetExtensions()
    //{
    //    foreach (var file in Directory.GetFiles(ExtensionDirectory, "*.dll"))
    //    {
    //        using (var reader = new DllReader(File.OpenRead(file)))
    //        {
    //            var attribute = reader.GetCustomAttribute<ExtensionInfoAttribute>();
    //            if (attribute != null)
    //            {
    //                yield return attribute;
    //            }
    //        }
    //    }
    //}
}
