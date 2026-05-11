using Core.Abstractions;

namespace Core.Cli.Help;

public interface IHelpRenderer
{
    string[] RenderRoot(IEnumerable<RuntimeHelpGroup> groups, uint? terminalWidth);

    string[] RenderGroup(RuntimeHelpGroup group, uint? terminalWidth);

    string[] RenderCommand(RuntimeHelpCommand command, uint? terminalWidth);
}

public sealed class RuntimeHelpRenderer : IHelpRenderer
{
    public string[] RenderRoot(IEnumerable<RuntimeHelpGroup> groups, uint? terminalWidth)
    {
        var lines = new List<string>
        {
            "Groups:"
        };

        foreach (var group in groups.OrderBy(static g => g.GroupPath, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"    {group.GroupPath} : {group.Summary}");
        }

        return [.. lines];
    }

    public string[] RenderGroup(RuntimeHelpGroup group, uint? terminalWidth)
    {
        var lines = new List<string>
        {
            "Group",
            $"    azv2 {group.GroupPath} : {group.Summary}",
            string.Empty
        };

        lines.Add("Commands:");

        if (group.Commands.Count == 0)
        {
            lines.Add("    (none)");
        }
        else
        {
            foreach (var command in group.Commands)
            {
                var tags = command.Tags.Length == 0
                    ? string.Empty
                    : $" [{string.Join(",", command.Tags)}]";
                lines.Add($"    {command.Name}{tags} : {command.Summary}");
            }
        }

        return [.. lines];
    }

    public string[] RenderCommand(RuntimeHelpCommand command, uint? terminalWidth)
    {
        var lines = new List<string>
        {
            "Command",
            $"    azv2 {command.CommandPath} : {command.Summary}",
            string.Empty,
            "Arguments"
        };

        if (command.Parameters.Count == 0)
        {
            lines.Add("    (none)");
        }
        else
        {
            foreach (var parameter in command.Parameters)
            {
                var aliases = parameter.Aliases.Length == 0
                    ? string.Empty
                    : $" -{string.Join(" -", parameter.Aliases)}";
                var required = parameter.Required ? " [Required]" : string.Empty;
                lines.Add($"    --{parameter.Name}{aliases}{required} : {parameter.Description}");
            }
        }

        if (command.Examples.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Examples");
            foreach (var example in command.Examples)
            {
                lines.Add($"    {example.Title}");
                lines.Add($"        {example.Command}");
            }
        }

        return [.. lines];
    }
}