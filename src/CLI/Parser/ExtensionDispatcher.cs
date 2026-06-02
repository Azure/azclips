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

    private readonly IExtensionResolver _resolver;
    private readonly Lazy<LeafIndex> _leafIndex;

    public ExtensionDispatcher()
        : this(new FileExtensionRegistryStore())
    {
    }

    private ExtensionDispatcher(FileExtensionRegistryStore store)
        : this(
            new ExtensionResolver(new ReconcilingStore(store)),
            () => LeafIndex.Build(BuiltInExtensions.Map, store.LoadReconciled()))
    {
    }

    /// <summary>Reconciled view of <see cref="FileExtensionRegistryStore"/> exposed through the <see cref="IExtensionRegistryStore"/> contract.</summary>
    private sealed class ReconcilingStore : IExtensionRegistryStore
    {
        private readonly FileExtensionRegistryStore _inner;
        public ReconcilingStore(FileExtensionRegistryStore inner) => _inner = inner;
        public ExtensionRegistry Load() => _inner.LoadReconciled();
        public void Save(ExtensionRegistry registry) => _inner.Save(registry);
    }

    public ExtensionDispatcher(IExtensionResolver resolver)
        : this(resolver, () => LeafIndex.Build(BuiltInExtensions.Map, new ExtensionRegistry()))
    {
    }

    /// <summary>Test-only constructor: inject both the verb resolver and an explicit leaf-index factory.</summary>
    public ExtensionDispatcher(IExtensionResolver resolver, Func<LeafIndex> leafIndexFactory)
    {
        _resolver = resolver;
        _leafIndex = new Lazy<LeafIndex>(leafIndexFactory);
    }

    public CommandInvocation Parse(string[] args)
    {
        if (args.Length == 0) throw new ArgumentException("Arguments cannot be empty.", nameof(args));
        var helpFlags = new[] { "--help", "-h", "help", "?", "-?" };
        if (args.Length == 1 && helpFlags.Contains(args[0], StringComparer.OrdinalIgnoreCase)) throw new DispatchHelpException(this);

        var verb = args[0];

        // Longest-prefix match against the (verb, path) leaf index; first hit wins.
        var nonFlagLeadingArgs = args
            .Skip(1)
            .TakeWhile(a => !a.StartsWith("-", StringComparison.Ordinal))
            .ToArray();

        ResolvedExtension? resolved = null;
        int matchedPathTokens = 0;

        for (int len = nonFlagLeadingArgs.Length; len >= 0; len--)
        {
            var path = string.Join(" ", nonFlagLeadingArgs, 0, len);
            if (_leafIndex.Value.TryGet(verb, path, out var leaf) && leaf is not null)
            {
                resolved = new ResolvedExtension(verb, leaf.PackageId, Version: null);
                matchedPathTokens = len;
                break;
            }
        }

        // Fallback to verb-only resolution
        if (resolved is null)
        {
            if (!_resolver.TryResolve(verb, out resolved) || resolved is null)
            {
                for (int len = nonFlagLeadingArgs.Length; len >= 0; len--)
                {
                    var path = string.Join(" ", nonFlagLeadingArgs, 0, len);
                    if (ExtensionHintTable.Default.TryGetPackageId(verb, path, out var hintedPackage))
                    {
                        throw new ExtensionNotInstalledException(verb, hintedPackage);
                    }
                }

                // Generic miss → OrchestratorEngine hands off to AI fallback.
                throw new KeyNotFoundException(
                    $"Command group '{verb}' not found. Run 'azv2 extension list' to see installed extensions.");
            }
        }

        // Strip Constants.cirrusPrefix; the loader re-prepends it.
        var extensionName = resolved.PackageId.StartsWith(Constants.cirrusPrefix, StringComparison.Ordinal)
            ? resolved.PackageId[Constants.cirrusPrefix.Length..]
            : resolved.PackageId;

        var extensionArgs = args.Skip(1 + matchedPathTokens).ToArray();
        var extensionParser = new ExtensionParser();

        string commandName;
        IDictionary<string, object> parameters;

        if (matchedPathTokens > 0)
        {
            var residualPositional = extensionArgs
                .TakeWhile(a => !a.StartsWith("-", StringComparison.Ordinal))
                .ToArray();
            var flagArgs = extensionArgs.Skip(residualPositional.Length).ToArray();
            (_, parameters) = extensionParser.Parse(flagArgs);
            if (residualPositional.Length > 0)
            {
                parameters[string.Empty] = new List<List<string>> { residualPositional.ToList() };
            }

            var matchedPath = string.Join(" ", nonFlagLeadingArgs, 0, matchedPathTokens);
            commandName = $"{verb} {matchedPath}";
        }
        else
        {
            (string subCommand, parameters) = extensionParser.Parse(extensionArgs);
            commandName = string.IsNullOrEmpty(subCommand) ? verb : $"{verb} {subCommand}";
        }

        return new CommandInvocation(extensionName, commandName, parameters, IsTransient: true, ModuleName: extensionName);
    }
}
