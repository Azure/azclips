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

    /// <summary>
    /// Adapter that wraps a <see cref="FileExtensionRegistryStore"/> and exposes only the
    /// reconciled (live) view through the <see cref="IExtensionRegistryStore"/> contract. The
    /// dispatcher's default chain uses this so both the leaf index and the verb-only fallback
    /// resolver see the same filtered registry (per the reconciler contract).
    /// </summary>
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

    /// <summary>
    /// Full constructor. Tests can supply both the verb resolver (legacy fallback) and an
    /// explicit leaf-index factory to exercise the longest-prefix dispatch logic in isolation.
    /// </summary>
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

        // Longest-prefix match against the (verb, path) leaf index. Walk back from the longest
        // possible path (all leading non-flag args after the verb) down to the empty path. First
        // hit wins. The dispatcher only needs the package owner here; the extension's
        // s_cliTable (full-path keyed) does the final lookup with the same key.
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

        // Fallback to verb-only resolution. Covers group help (`azv2 vm --help`) where the
        // user typed only the verb, and registry entries that haven't been migrated to declare a
        // root contribution. Leaf-level conflicts surface via _leafIndex above; this path is the
        // "I know the namespace but not the leaf" branch.
        if (resolved is null)
        {
            if (!_resolver.TryResolve(verb, out resolved) || resolved is null)
            {
                // Consult the single ExtensionHintTable (build-time embedded map of every
                // in-tree verb + leaf to its owning package). Walk back from the longest
                // possible path to the bare verb; first hit produces a precise
                // `extension add` suggestion and bypasses the AI fallback. Generic misses fall
                // through to OrchestratorEngine's AI agent.
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

        // Loader compatibility: AssemblyLoadingService prepends "Microsoft.Azure.Cirrus.Extensions."
        // to its argument. Strip that prefix from the package id to obtain the short name today.
        // Follow-up: have the loader accept full package ids and remove this asymmetry.
        const string cirrusPrefix = "Microsoft.Azure.Cirrus.Extensions.";
        var extensionName = resolved.PackageId.StartsWith(cirrusPrefix, StringComparison.Ordinal)
            ? resolved.PackageId[cirrusPrefix.Length..]
            : resolved.PackageId;

        // After the verb (and any matched leaf-path tokens), what's left is the extension's input.
        // Behavior depends on whether we hit an exact leaf:
        //   • Exact leaf match (matchedPathTokens > 0): any further non-flag tokens are
        //     positional residual — bound to the empty parameter key (e.g.
        //     `extension remove vm` routes to (`extension`, `remove`) with `vm` surfaced as a
        //     positional). Only flag tokens go through ExtensionParser.
        //   • Bare verb / verb-only fallback (matchedPathTokens == 0): we don't yet know the path,
        //     so ExtensionParser splits leading non-flag tokens into the subCommand which we
        //     append to form the full-path commandName.
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
                // ExtensionParser bins positional arguments under the empty key as a list of
                // value lists; mirror that shape so the downstream argument binder treats them
                // identically to any other positional input.
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
