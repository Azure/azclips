using System.Reflection;
using Core.Abstractions;

namespace Core.Cli.Help.Templates;

/// <summary>
/// Renders help for a command group (extension) showing all available commands.
/// Displayed when the user runs: azv2 group --help
/// </summary>
public class CommandGroupHelpTemplate
{
    public uint Indent { get; set; } = 4;

    /// <summary>
    /// Renders the command group help output.
    /// </summary>
    /// <param name="groupName">The CLI group name (e.g., "group", "login").</param>
    /// <param name="extensionIndex">The extension's command index for enumerating commands.</param>
    /// <param name="executableName">The CLI executable name (e.g., "azv2").</param>
    /// <param name="terminalWidth">Terminal width for word-wrapping; null for no wrapping.</param>
    public string[] Render(
        string groupName,
        IExtensionIndex extensionIndex,
        string? executableName = null,
        uint? terminalWidth = null)
    {
        var exe = executableName ?? "azv2";
        var renderedLines = new List<string>
        {
            "Group",
            new string(' ', (int)Indent) + $"{exe} {groupName.ToLowerInvariant()}"
        };

        var helpTable = new MultiGroupHelpTable();
        var commands = extensionIndex.GetAllCommands(ClientType.CLI).ToList();
        var subGroupSet = new HashSet<string>();
        var leafCommands = new List<ICommandMetadata>();

        foreach (var cmd in commands)
        {
            // Read the CLI command name from CommandInfoAttribute on the type
            var cmdInfo = cmd.CommandType.GetCustomAttribute<CommandInfoAttribute>();
            var commandName = cmdInfo?.Name ?? cmd.Name;

            // If the name contains a space, the first token is a subgroup
            var spaceIndex = commandName.IndexOf(' ');
            if (spaceIndex >= 0)
            {
                subGroupSet.Add(commandName[..spaceIndex]);
            }
            else
            {
                leafCommands.Add(cmd);
            }
        }

        foreach (var subGroupName in subGroupSet.OrderBy(n => n))
        {
            helpTable.AddLine(new HelpLine(subGroupName, [], group: "Subgroups"));
        }

        foreach (var cmd in leafCommands.OrderBy(c =>
            c.CommandType.GetCustomAttribute<CommandInfoAttribute>()?.Name ?? c.Name))
        {
            var cmdInfo = cmd.CommandType.GetCustomAttribute<CommandInfoAttribute>();
            var commandName = cmdInfo?.Name ?? cmd.Name;
            var displayName = string.IsNullOrEmpty(commandName) ? "(default)" : commandName;
            var description = string.IsNullOrEmpty(cmdInfo?.Description)
                ? Array.Empty<string>()
                : cmdInfo!.Description.Split('\n');
            helpTable.AddLine(new HelpLine(displayName, description, group: "Commands"));
        }

        var renderedTableGroups = helpTable.Render(
            indent: Indent,
            offset: Indent,
            maxWidth: terminalWidth,
            minDescriptionWidth: 20
        );

        foreach (var renderedTableGroup in renderedTableGroups)
        {
            if (renderedTableGroup.GroupName != null)
            {
                renderedLines.Add(string.Empty);
                renderedLines.Add($"{renderedTableGroup.GroupName}:");
            }
            renderedLines.AddRange(renderedTableGroup.RenderedLines);
        }

        renderedLines.Add(string.Empty);
        renderedLines.Add($"Use '{exe} {groupName.ToLowerInvariant()} <command> --help' to see command details.");

        return [.. renderedLines];
    }
}
