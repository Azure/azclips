using System.Reflection;
using Core.Abstractions;
using Core.Cli.Parser;

namespace Core.Cli.Help.Templates;

/// <summary>
/// Renders help for a specific command showing its description and all arguments.
/// Displayed when the user runs: azv2 group list --help
/// </summary>
public class CommandHelpTemplate
{
    public uint Indent { get; set; } = 4;

    /// <summary>
    /// Renders the per-command help output.
    /// </summary>
    /// <param name="groupName">The CLI group name (e.g., "group", "login").</param>
    /// <param name="commandMetadata">Metadata for the matched command.</param>
    /// <param name="executableName">The CLI executable name (e.g., "azv2").</param>
    /// <param name="terminalWidth">Terminal width for word-wrapping; null for no wrapping.</param>
    public string[] Render(
        string groupName,
        ICommandMetadata commandMetadata,
        string? executableName = null,
        uint? terminalWidth = null)
    {
        var exe = executableName ?? "azv2";
        var cmdInfo = commandMetadata.CommandType.GetCustomAttribute<CommandInfoAttribute>();
        var commandName = cmdInfo?.Name ?? commandMetadata.Name;
        var description = cmdInfo?.Description ?? "";

        var commandParts = new List<string> { exe, groupName.ToLowerInvariant() };
        if (!string.IsNullOrEmpty(commandName))
        {
            commandParts.Add(commandName);
        }
        var fullCommand = string.Join(" ", commandParts);

        var renderedLines = new List<string>
        {
            "Command",
            new string(' ', (int)Indent) + fullCommand + (string.IsNullOrEmpty(description) ? "" : $" : {description}"),
            string.Empty,
            "Arguments",
        };

        var helpTable = new MultiGroupHelpTable();

        // Reflect on command type properties with ArgumentInfoAttribute
        foreach (var property in commandMetadata.CommandType.GetProperties())
        {
            var attribute = property.GetCustomAttribute<ArgumentInfoAttribute>();
            if (attribute == null) continue;

            // Use explicit Aliases if present; otherwise fall back to auto-generated kebab-case name
            var aliases = attribute.Aliases
                .Where(alias => !string.IsNullOrEmpty(alias))
                .ToArray();
            if (aliases.Length == 0)
            {
                aliases = [PropertyParser.GetPropertyRawAlias(property)];
            }

            string[] shortCol = aliases
                .Where(a => a.Length == 1)
                .Select(a => $"-{a}")
                .ToArray();
            string[] namesCol = aliases
                .Where(a => a.Length > 1)
                .Select(a => $"--{a}")
                .ToArray();

            var tags = new List<string>();
            if (attribute.Required)
            {
                tags.Add("[Required]");
            }

            string[] desc = string.IsNullOrEmpty(attribute.Description)
                ? []
                : attribute.Description.Split('\n');

            // Detail line for enum values
            string[] detail = attribute.Enums.Length > 0
                ? [$"Allowed values: {string.Join(", ", attribute.Enums)}"]
                : [];

            helpTable.AddLine(new HelpLine(shortCol, namesCol, [.. tags], desc, detail));
        }

        var renderedTableGroups = helpTable.Render(
            indent: Indent,
            offset: Indent,
            maxWidth: terminalWidth,
            minDescriptionWidth: 20
        );

        foreach (var renderedTableGroup in renderedTableGroups)
        {
            renderedLines.AddRange(renderedTableGroup.RenderedLines);
        }

        return [.. renderedLines];
    }
}
