using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Core.Cli.Exceptions;

namespace Core.Cli.Parser;

public class ExtensionDispatcher //: IParser
{
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

    public Implementation.CLICommandInvocation Parse(string[] args)
    {
        if (args.Length == 0) throw new ArgumentException("Arguments cannot be empty.", nameof(args));
        var helpFlags = new[] { "--help", "-h", "help", "?", "-?" };
        if (args.Length == 1 && helpFlags.Contains(args[0], StringComparer.OrdinalIgnoreCase)) throw new DispatchHelpException(this);
        string extensionName = CommandGroupIndex.getExtensionNameByCommandGroupName($"{char.ToUpperInvariant(args[0][0])}{args[0][1..]}");
        // string dllName = $"Microsoft.Azure.Cirrus.Extensions.{extensionName}";
        // string dllPath = Path.Combine(ExtensionDirectory, dllName, $"{dllName}.dll");
        // if (!File.Exists(dllPath)) throw new FileNotFoundException($"Extension assembly not found: {dllPath}");

        var extensionParser = new ExtensionParser();
        (string commandName, IDictionary<string, object> parameters) = extensionParser.Parse(args[1..]);
        return new Implementation.CLICommandInvocation(extensionName, commandName, parameters);
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
