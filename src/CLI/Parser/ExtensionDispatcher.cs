using System.Reflection;
using System.Runtime.Loader;
using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Extensions;

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

    // Default to the file-backed store + built-in seed; tests / DI can substitute via the overload.
    private readonly IExtensionResolver _resolver;

    public ExtensionDispatcher()
        : this(new ExtensionResolver(new FileExtensionRegistryStore()))
    {
    }

    public ExtensionDispatcher(IExtensionResolver resolver)
    {
        _resolver = resolver;
    }

    public CommandInvocation Parse(string[] args)
    {
        if (args.Length == 0) throw new ArgumentException("Arguments cannot be empty.", nameof(args));
        var helpFlags = new[] { "--help", "-h", "help", "?", "-?" };
        if (args.Length == 1 && helpFlags.Contains(args[0], StringComparer.OrdinalIgnoreCase)) throw new DispatchHelpException(this);

        if (!_resolver.TryResolve(args[0], out var resolved) || resolved is null)
        {
            // Phase 4 #13: consult the build-time hint table. If the verb maps to a known
            // first-party extension, tell the user how to install it and bypass the AI fallback
            // (intent is unambiguous). Otherwise let the generic "not found" go to the AI.
            if (VerbHintTable.Default.TryGetPackageId(args[0], out var hintedPackage))
            {
                throw new ExtensionNotInstalledException(args[0], hintedPackage);
            }

            // Generic miss → OrchestratorEngine hands off to AI fallback.
            throw new KeyNotFoundException(
                $"Command group '{args[0]}' not found. Run 'azv2 extension list' to see installed extensions.");
        }

        // Loader compatibility: AssemblyLoadingService prepends "Microsoft.Azure.Cirrus.Extensions."
        // to its argument. Strip that prefix from the package id to obtain the short name today.
        // Phase 5 follow-up: have the loader accept full package ids and remove this asymmetry.
        const string cirrusPrefix = "Microsoft.Azure.Cirrus.Extensions.";
        var extensionName = resolved.PackageId.StartsWith(cirrusPrefix, StringComparison.Ordinal)
            ? resolved.PackageId[cirrusPrefix.Length..]
            : resolved.PackageId;

        var extensionParser = new ExtensionParser();
        (string commandName, IDictionary<string, object> parameters) = extensionParser.Parse(args[1..]);
        return new CommandInvocation(extensionName, commandName, parameters, IsTransient: true, ModuleName: extensionName);
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
