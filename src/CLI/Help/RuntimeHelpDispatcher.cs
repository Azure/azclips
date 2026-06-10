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
/// Help is verb-scoped, not package-scoped. <see cref="RenderRoot"/> enumerates every
/// installed extension's <see cref="IExtensionIndex.GetVerbs"/> to discover all root
/// groups; <see cref="RenderExtensionHelp"/> uses the verb on
/// <see cref="RuntimeExtensionHelpException"/> to locate the owning package via
/// <see cref="IExtensionResolver"/>.
/// </remarks>
public sealed class RuntimeHelpDispatcher
{
    private readonly IHelpRenderer _renderer;
    private readonly IAssemblyLoadingService _assemblyLoading;
    private readonly IExtensionResolver _extensionResolver;
    private readonly TextWriter _output;

    public RuntimeHelpDispatcher(IAssemblyLoadingService assemblyLoading, IExtensionResolver extensionResolver)
        : this(assemblyLoading, extensionResolver, new RuntimeHelpRenderer(), Console.Out)
    {
    }

    internal RuntimeHelpDispatcher(
        IAssemblyLoadingService assemblyLoading,
        IExtensionResolver extensionResolver,
        IHelpRenderer renderer,
        TextWriter output)
    {
        _assemblyLoading = assemblyLoading;
        _extensionResolver = extensionResolver;
        _renderer = renderer;
        _output = output;
    }

    /// <summary>Renders the top-level <c>azv2</c> help (every installed extension's verb roots).</summary>
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
            throw new KeyNotFoundException($"No installed extension owns verb '{helpException.Verb}'.");
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

    private IEnumerable<RuntimeHelpGroup> EnumerateAllVerbRoots()
    {
        IList<Tuple<string, string, Func<System.Reflection.Assembly>>>? loaded;
        try
        {
            loaded = _assemblyLoading.LoadAllExtensions();
        }
        catch
        {
            // Discovery failures (provider not implemented, registry missing, etc.) must
            // not crash root help. Render the empty frame instead.
            yield break;
        }

        if (loaded == null)
        {
            yield break;
        }

        foreach (var (_, _, loader) in loaded)
        {
            IExtensionIndex index;
            try
            {
                index = loader().GetExtensionIndex();
            }
            catch
            {
                continue;
            }

            foreach (var verb in index.GetVerbs())
            {
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
