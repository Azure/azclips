using System.Reflection;
using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Extensions;
using Core.Runtime;

namespace Core.Cli.Help;

/// <summary>
/// Renders root, group, and command help to the console. Encapsulates the help
/// orchestration that used to live in <c>Program.Main</c>'s catch blocks: loading
/// extension assemblies, projecting them through <see cref="ExtensionIndexHelpAdapter"/>
/// (one adapter per verb), and dispatching to <see cref="IHelpRenderer"/>.
/// </summary>
/// <remarks>
/// <para>
/// Help is verb-scoped, not package-scoped. <see cref="RenderRoot"/> enumerates every
/// known package (built-ins from <see cref="BuiltInExtensions.Map"/> + every entry in
/// <see cref="IExtensionRegistryStore"/>), then projects each through
/// <see cref="IExtensionIndex.GetVerbs"/> to discover root groups; this is what makes
/// multi-verb extensions like Compute surface as both <c>vm</c> and <c>disk</c> roots.
/// </para>
/// <para>
/// <see cref="RenderExtensionHelp"/> resolves verb→package via <see cref="IExtensionResolver"/>;
/// on a miss it consults <see cref="ExtensionHintTable.Default"/> so <c>azv2 vm --help</c>
/// (when Compute is uninstalled) yields a precise "install it with: azv2 extension add --name Compute"
/// hint instead of a bare <see cref="KeyNotFoundException"/>.
/// </para>
/// </remarks>
public sealed class RuntimeHelpDispatcher
{
    private readonly IHelpRenderer _renderer;
    private readonly IAssemblyLoadingService _assemblyLoading;
    private readonly IExtensionResolver _extensionResolver;
    private readonly IExtensionRegistryStore _registryStore;
    private readonly IReadOnlyDictionary<string, BuiltInExtension> _builtIns;
    private readonly ExtensionHintTable _hintTable;
    private readonly TextWriter _output;

    public RuntimeHelpDispatcher(
        IAssemblyLoadingService assemblyLoading,
        IExtensionResolver extensionResolver,
        IExtensionRegistryStore registryStore)
        : this(
            assemblyLoading,
            extensionResolver,
            registryStore,
            BuiltInExtensions.Map,
            ExtensionHintTable.Default,
            new RuntimeHelpRenderer(),
            Console.Out)
    {
    }

    internal RuntimeHelpDispatcher(
        IAssemblyLoadingService assemblyLoading,
        IExtensionResolver extensionResolver,
        IExtensionRegistryStore registryStore,
        IReadOnlyDictionary<string, BuiltInExtension> builtIns,
        ExtensionHintTable hintTable,
        IHelpRenderer renderer,
        TextWriter output)
    {
        _assemblyLoading = assemblyLoading;
        _extensionResolver = extensionResolver;
        _registryStore = registryStore;
        _builtIns = builtIns;
        _hintTable = hintTable;
        _renderer = renderer;
        _output = output;
    }

    /// <summary>Renders the top-level <c>azv2</c> help: every verb root from every known package.</summary>
    public int RenderRoot()
    {
        var rootGroups = EnumerateAllVerbRoots().ToArray();
        WriteLines(_renderer.RenderRoot(rootGroups, TerminalWidth()));
        return 0;
    }

    /// <summary>Renders help for a group or command path raised via <see cref="RuntimeExtensionHelpException"/>.</summary>
    public int RenderExtensionHelp(RuntimeExtensionHelpException helpException)
    {
        if (!_extensionResolver.TryResolve(helpException.Verb, out var resolved) || resolved is null)
        {
            // Verb is unknown to the resolver. If the embedded hint table knows it,
            // surface the structured "extension not installed" message — same UX as
            // the execute path (see ExtensionDispatcher.Parse).
            if (_hintTable.TryGetPackageId(helpException.Verb, out var hintedPackage))
            {
                throw new ExtensionNotInstalledException(helpException.Verb, hintedPackage);
            }

            // Truly unknown verb (no install hint either). Surface as a structured error so the
            // top-level CirrusErrorException catch renders it cleanly.
            throw new CirrusErrorException(
                code: "VerbNotFound",
                exitCode: 4,
                message: $"No installed extension owns verb '{helpException.Verb}'. Run 'azv2 extension list' to see installed extensions.",
                details: new Dictionary<string, object?> { ["verb"] = helpException.Verb });
        }

        var extensionName = StripPackagePrefix(resolved.PackageId);
        var assembly = _assemblyLoading.LoadExtension(extensionName);
        var helpProvider = new ExtensionIndexHelpAdapter(assembly.GetExtensionIndex(), helpException.Verb);

        IEnumerable<string> lines;
        if (helpException.IsCommandHelp)
        {
            var command = helpProvider.GetCommandHelp(helpException.Path)
                ?? throw new KeyNotFoundException($"Command help not found for path '{string.Join(" ", helpException.Path)}'.");
            lines = _renderer.RenderCommand(command, TerminalWidth());
        }
        else
        {
            // A verb root can also be a runnable command (e.g. "azv2 login" is both).
            var rootCommand = helpProvider.GetCommandHelp(helpException.Path);
            if (rootCommand != null)
            {
                lines = _renderer.RenderCommand(rootCommand, TerminalWidth());
            }
            else
            {
                var group = helpProvider.GetGroupHelp(helpException.Path)
                    ?? throw new KeyNotFoundException($"Group help not found for path '{string.Join(" ", helpException.Path)}'.");
                lines = _renderer.RenderGroup(group, TerminalWidth());
            }
        }

        WriteLines(lines);
        return 0;
    }

    private void WriteLines(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            _output.WriteLine(line);
        }
    }

    /// <summary>
    /// Yields one <see cref="RuntimeHelpGroup"/> per (package, verb) pair we can load. Package
    /// sources are deduplicated across built-ins and the on-disk registry.
    /// </summary>
    private IEnumerable<RuntimeHelpGroup> EnumerateAllVerbRoots()
    {
        var seenVerbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var packageId in EnumerateKnownPackageIds())
        {
            var extensionName = StripPackagePrefix(packageId);

            IExtensionIndex index;
            try
            {
                index = _assemblyLoading.LoadExtension(extensionName).GetExtensionIndex();
            }
            catch
            {
                // A registered package whose assembly is missing from disk is no reason to
                // crash root help; skip it and continue.
                continue;
            }

            foreach (var verb in index.GetVerbs())
            {
                if (!seenVerbs.Add(verb))
                {
                    // Built-ins win over registry entries for the same verb (defensive — `extension add`
                    // already refuses colliding verbs, but the assembly may still ship in both lists).
                    continue;
                }

                RuntimeHelpGroup? root;
                try
                {
                    root = new ExtensionIndexHelpAdapter(index, verb).GetRootGroup();
                }
                catch
                {
                    continue;
                }

                yield return root;
            }
        }
    }

    /// <summary>
    /// Returns every package id Cirrus might be able to load: built-ins first (so they win on
    /// verb conflicts), then everything in the on-disk registry.
    /// </summary>
    private IEnumerable<string> EnumerateKnownPackageIds()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var builtIn in _builtIns.Values)
        {
            if (seen.Add(builtIn.PackageId))
            {
                yield return builtIn.PackageId;
            }
        }

        ExtensionRegistry registry;
        try
        {
            registry = _registryStore.Load();
        }
        catch
        {
            yield break;
        }

        foreach (var packageId in registry.Extensions.Keys)
        {
            if (seen.Add(packageId))
            {
                yield return packageId;
            }
        }
    }

    /// <summary>Strips the <c>Microsoft.Azure.Cirrus.Extensions.</c> prefix that the loader re-prepends.</summary>
    private static string StripPackagePrefix(string packageId)
        => packageId.StartsWith(Constants.cirrusPrefix, StringComparison.Ordinal)
            ? packageId[Constants.cirrusPrefix.Length..]
            : packageId;

    private static uint? TerminalWidth()
    {
        try
        {
            var width = (uint)Console.WindowWidth;
            return width > 0 ? width : null;
        }
        catch
        {
            return null;
        }
    }
}
