using Core.Cli.Parser;

namespace Core.Cli.Help.Templates;

public class CommandGroupHelpTemplate
{
    public uint Indent { get; set; } = 4;

    public string[] Render(
        ExtensionParser extensionParser,
        string? executableName,
        string prefix,
        uint? terminalWidth = null
    )
    {
        var extensionName = extensionParser.ExtensionName;
        var renderedLines = new List<string>
        {
            $"Group",
            new string(' ', (int)Indent) + $"{executableName ?? "[EXE]"} {extensionName} {prefix}"
        };
        var helpTable = new MultiGroupHelpTable();
        var subCommandParsers = extensionParser.GetCommands(prefix);
        var subGroupSet = new HashSet<string>();
        var subCommands = new List<CommandParser>();
        foreach (var subCommandParser in subCommandParsers)
        {
            var commandName = subCommandParser.CommandInfo.Name;
            var subNameInGroup = commandName.Substring(prefix.Length).TrimStart(' ');
            var firstSpaceIndex = subNameInGroup.IndexOf(' ');
            if (firstSpaceIndex >= 0)
            {
                var subGroupName = subNameInGroup.Substring(0, firstSpaceIndex);
                subGroupSet.Add(subGroupName);
            }
            else
            {
                subCommands.Add(subCommandParser);
            }
        }
        foreach (var subGroupName in subGroupSet.OrderBy(name => name))
        {
            helpTable.AddLine(new HelpLine(subGroupName, [], group: "Subgroups"));
        }
        foreach (var subCommandParser in subCommands.OrderBy(cmd => cmd.CommandInfo.Name))
        {
            var commandName = subCommandParser.CommandInfo.Name;
            var subNameInGroup = commandName.Substring(prefix.Length).TrimStart(' ');
            var description = string.IsNullOrEmpty(subCommandParser.CommandInfo.Description)
                ? []
                : subCommandParser.CommandInfo.Description.Split('\n');
            helpTable.AddLine(new HelpLine(subNameInGroup, description, group: "Commands"));
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
        return [.. renderedLines];
    }
}
