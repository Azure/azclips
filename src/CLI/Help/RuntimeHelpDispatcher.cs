using Core.Abstractions;
using Core.Cli.Exceptions;
using Core.Cli.Parser;
using Core.Runtime;

namespace Core.Cli.Help;

/// <summary>
/// Renders root, group, and command help to the console. Encapsulates the help
/// orchestration that used to live in <c>Program.Main</c>'s catch blocks:
/// loading extension assemblies, projecting them through
/// <see cref="ExtensionIndexHelpAdapter"/>, and dispatching to <see cref="IHelpRenderer"/>.
/// </summary>
public sealed class RuntimeHelpDispatcher
{
    private readonly IHelpRenderer _renderer;
    private readonly IAssemblyLoadingService _assemblyLoading;
    private readonly TextWriter _output;

    public RuntimeHelpDispatcher(IAssemblyLoadingService assemblyLoading)
        : this(assemblyLoading, new RuntimeHelpRenderer(), Console.Out)
    {
    }

    internal RuntimeHelpDispatcher(IAssemblyLoadingService assemblyLoading, IHelpRenderer renderer, TextWriter output)
    {
        _assemblyLoading = assemblyLoading;
        _renderer = renderer;
        _output = output;
    }

    /// <summary>Renders the top-level <c>azv2</c> help (every registered extension's root groups).</summary>
    public int RenderRoot()
    {
        var rootGroups = CommandGroupIndex.GetRegisteredExtensionNames()
            .SelectMany(TryGetRootGroups)
            .ToArray();

        WriteLines(_renderer.RenderRoot(rootGroups, TerminalWidth()));
        return 0;
    }

    /// <summary>Renders help for a group or command path raised via <see cref="RuntimeExtensionHelpException"/>.</summary>
    public int RenderExtensionHelp(RuntimeExtensionHelpException helpException)
    {
        var assembly = _assemblyLoading.LoadExtension(helpException.ExtensionName);
        var helpProvider = new ExtensionIndexHelpAdapter(assembly.GetExtensionIndex(), helpException.ExtensionName);

        IEnumerable<string> lines;
        if (helpException.IsCommandHelp)
        {
            var command = helpProvider.GetCommandHelp(helpException.Path)
                ?? throw new KeyNotFoundException($"Command help not found for path '{string.Join(" ", helpException.Path)}'.");
            lines = _renderer.RenderCommand(command, TerminalWidth());
        }
        else
        {
            // A group root can also be a runnable command (e.g. "login" is both).
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

    private IEnumerable<RuntimeHelpGroup> TryGetRootGroups(string extensionName)
    {
        try
        {
            var assembly = _assemblyLoading.LoadExtension(extensionName);
            var helpProvider = new ExtensionIndexHelpAdapter(assembly.GetExtensionIndex(), extensionName);
            return helpProvider.GetRootGroups();
        }
        catch
        {
            return [];
        }
    }

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
