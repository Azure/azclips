// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli.Parser;

namespace Core.Cli.Help;

public interface IHelpRenderer
{
    string[] RenderRoot(IEnumerable<RuntimeHelpGroup> groups, uint? terminalWidth);

    string[] RenderGroup(RuntimeHelpGroup group, uint? terminalWidth);

    string[] RenderCommand(RuntimeHelpCommand command, uint? terminalWidth);

    string[] RenderGroupsSection(IEnumerable<RuntimeHelpGroup> groups, uint? terminalWidth);

    string[] RenderGroupIdentitySection(RuntimeHelpGroup group, uint? terminalWidth);

    string[] RenderSubgroupsSection(IReadOnlyList<RuntimeHelpGroupSummary> subgroups, uint? terminalWidth);

    string[] RenderCommandsSection(IReadOnlyList<RuntimeHelpCommandSummary> commands, uint? terminalWidth);

    string[] RenderCommandIdentitySection(RuntimeHelpCommand command, uint? terminalWidth);

    string[] RenderParameterSections(IReadOnlyList<RuntimeHelpParameter> parameters, uint? terminalWidth);

    string[] RenderParameterGroupSection(string header, IReadOnlyList<RuntimeHelpParameter> parameters, uint? terminalWidth);

    string[] RenderExamplesSection(IReadOnlyList<RuntimeHelpExample> examples, uint? terminalWidth);

    string[] RenderGlobalArgumentsSection(uint? terminalWidth);
}

public sealed class RuntimeHelpRenderer : IHelpRenderer
{
    private const string Bold = "\u001b[1m";
    private const string Reset = "\u001b[0m";

    private readonly bool _boldHeaders;

    public RuntimeHelpRenderer(bool boldHeaders = false)
    {
        _boldHeaders = boldHeaders;
    }

    private string SectionHeader(string title)
    {
        var header = $"# {title.ToUpperInvariant()}";
        return _boldHeaders ? $"{Bold}{header}{Reset}" : header;
    }

    public string[] RenderRoot(IEnumerable<RuntimeHelpGroup> groups, uint? terminalWidth)
        => RenderGroupsSection(groups, terminalWidth);

    public string[] RenderGroup(RuntimeHelpGroup group, uint? terminalWidth)
        => ComposeSections(
            RenderGroupIdentitySection(group, terminalWidth),
            RenderSubgroupsSection(group.Subgroups, terminalWidth),
            RenderCommandsSection(group.Commands, terminalWidth),
            RenderGlobalArgumentsSection(terminalWidth));

    public string[] RenderCommand(RuntimeHelpCommand command, uint? terminalWidth)
        => ComposeSections(
            RenderCommandIdentitySection(command, terminalWidth),
            RenderParameterSections(command.Parameters, terminalWidth),
            RenderExamplesSection(command.Examples, terminalWidth),
            RenderGlobalArgumentsSection(terminalWidth));

    public string[] RenderGroupsSection(IEnumerable<RuntimeHelpGroup> groups, uint? terminalWidth)
    {
        var lines = new List<string>
        {
            SectionHeader("Groups"),
        };

        var ordered = groups.OrderBy(static group => group.GroupPath, StringComparer.OrdinalIgnoreCase).ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            if (index > 0)
            {
                lines.Add(string.Empty);
            }

            AppendNameAndSummary(lines, ordered[index].GroupPath, ordered[index].Summary);
        }

        return [.. lines];
    }

    public string[] RenderGroupIdentitySection(RuntimeHelpGroup group, uint? terminalWidth)
    {
        var lines = new List<string> { SectionHeader("Group") };
        AppendNameAndSummary(lines, $"az {group.GroupPath}", group.Summary);
        return [.. lines];
    }

    public string[] RenderSubgroupsSection(IReadOnlyList<RuntimeHelpGroupSummary> subgroups, uint? terminalWidth)
    {
        if (subgroups.Count == 0)
        {
            return [];
        }

        var lines = new List<string> { SectionHeader("Subgroups") };
        for (var index = 0; index < subgroups.Count; index++)
        {
            if (index > 0)
            {
                lines.Add(string.Empty);
            }

            AppendNameAndSummary(lines, subgroups[index].Name, subgroups[index].Summary);
        }

        return [.. lines];
    }

    public string[] RenderCommandsSection(IReadOnlyList<RuntimeHelpCommandSummary> commands, uint? terminalWidth)
    {
        var lines = new List<string> { SectionHeader("Commands") };
        if (commands.Count == 0)
        {
            lines.Add("(none)");
            return [.. lines];
        }

        for (var index = 0; index < commands.Count; index++)
        {
            if (index > 0)
            {
                lines.Add(string.Empty);
            }

            var command = commands[index];
            var tags = command.Tags.Length == 0
                ? string.Empty
                : $" [{string.Join(",", command.Tags)}]";
            AppendNameAndSummary(lines, $"{command.Name}{tags}", command.Summary);
        }

        return [.. lines];
    }

    public string[] RenderCommandIdentitySection(RuntimeHelpCommand command, uint? terminalWidth)
    {
        var lines = new List<string> { SectionHeader("Command") };
        AppendNameAndSummary(lines, $"az {command.CommandPath}", command.Summary);
        return [.. lines];
    }

    private static void AppendNameAndSummary(List<string> lines, string name, string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            lines.Add(name);
            return;
        }

        lines.Add($"{name}:");
        lines.Add(summary);
    }

    public string[] RenderGlobalArgumentsSection(uint? terminalWidth)
    {
        var lines = new List<string> { SectionHeader("Global Arguments") };
        for (var i = 0; i < KnownGlobalFlags.All.Count; i++)
        {
            if (i > 0)
            {
                lines.Add(string.Empty);
            }
            var flag = KnownGlobalFlags.All[i];
            var aliases = flag.Aliases.Count == 0
                ? string.Empty
                : " " + string.Join(" ", flag.Aliases);
            lines.Add($"{flag.Name}{aliases}:");
            lines.Add(flag.Summary);
        }

        return [.. lines];
    }

    // Emits an "Arguments" section for ungrouped params, then a section per named GroupName in
    // alphabetical order. Sections with no params are skipped. When Parameters is empty, the
    // single "Arguments" header + "(none)" placeholder is emitted (preserves prior behavior for
    // no-param commands like `az logout`).
    public string[] RenderParameterSections(IReadOnlyList<RuntimeHelpParameter> parameters, uint? terminalWidth)
    {
        if (parameters.Count == 0)
        {
            return [SectionHeader("Arguments"), "(none)"];
        }

        var defaultBucket = new List<RuntimeHelpParameter>();
        var groupedBuckets = new SortedDictionary<string, List<RuntimeHelpParameter>>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in parameters)
        {
            if (string.IsNullOrEmpty(parameter.GroupName))
            {
                defaultBucket.Add(parameter);
                continue;
            }

            if (!groupedBuckets.TryGetValue(parameter.GroupName, out var bucket))
            {
                bucket = [];
                groupedBuckets[parameter.GroupName] = bucket;
            }
            bucket.Add(parameter);
        }

        var sections = new List<string[]>();
        if (defaultBucket.Count > 0)
        {
            sections.Add(RenderParameterGroupSection("Arguments", defaultBucket, terminalWidth));
        }

        foreach (var (groupName, bucket) in groupedBuckets)
        {
            sections.Add(RenderParameterGroupSection(groupName, bucket, terminalWidth));
        }

        return ComposeSections([.. sections]);
    }

    public string[] RenderParameterGroupSection(
        string header,
        IReadOnlyList<RuntimeHelpParameter> parameters,
        uint? terminalWidth)
    {
        var lines = new List<string> { SectionHeader(header) };
        for (var index = 0; index < parameters.Count; index++)
        {
            if (index > 0)
            {
                lines.Add(string.Empty);
            }

            var parameter = parameters[index];
            var aliases = parameter.Aliases.Length == 0
                ? string.Empty
                : $" -{string.Join(" -", parameter.Aliases)}";
            var required = parameter.Required ? " [Required]" : string.Empty;
            lines.Add($"--{parameter.Name}{aliases}{required}:");
            lines.Add(parameter.Description);

            if (!string.IsNullOrEmpty(parameter.ValueSources))
            {
                var suffix = parameter.ValueSources.EndsWith('.') ? string.Empty : ".";
                lines.Add($"Values from: {parameter.ValueSources}{suffix}");
            }
        }

        return [.. lines];
    }

    public string[] RenderExamplesSection(IReadOnlyList<RuntimeHelpExample> examples, uint? terminalWidth)
    {
        if (examples.Count == 0)
        {
            return [];
        }

        var lines = new List<string> { SectionHeader("Examples") };
        for (var index = 0; index < examples.Count; index++)
        {
            if (index > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add($"{index + 1}. {examples[index].Title}");
            lines.Add(examples[index].Command);
        }

        return [.. lines];
    }

    private static string[] ComposeSections(params IEnumerable<string>[] sections)
    {
        var lines = new List<string>();
        foreach (var section in sections)
        {
            var sectionLines = section as IReadOnlyCollection<string> ?? section.ToArray();
            if (sectionLines.Count == 0)
            {
                continue;
            }

            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.AddRange(sectionLines);
        }

        return [.. lines];
    }
}