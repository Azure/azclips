using Core.Cli.Parser;

namespace Core.Cli.Help.Templates;

/// <summary>
/// Renders top-level CLI help showing all available command groups.
/// Displayed when the user runs: azv2 --help
/// </summary>
public class DispatchHelpTemplate
{
    public uint Indent { get; set; } = 4;

    public string[] Render(string? executableName = null, uint? terminalWidth = null)
    {
        var exe = executableName ?? "azv2";
        var renderedLines = new List<string>
        {
            "Command Line Interface Help",
            string.Empty,
            new string(' ', (int)Indent) + $"{exe} [command-group] [command] [options]",
        };

        var helpTable = new MultiGroupHelpTable();

        foreach (var group in CommandGroupIndex.GetAllGroups().OrderBy(g => g.GroupName))
        {
            var description = string.IsNullOrEmpty(group.Description)
                ? Array.Empty<string>()
                : group.Description.Split('\n');
            helpTable.AddLine(new HelpLine(group.GroupName.ToLowerInvariant(), description, group: "Command Groups"));
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
