// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli.Parser;

namespace Core.Cli.Help;

public interface IHelpRenderer
{
    string[] RenderRoot(IEnumerable<RuntimeHelpGroup> groups, uint? terminalWidth);

    string[] RenderGroup(RuntimeHelpGroup group, uint? terminalWidth);

    string[] RenderCommand(RuntimeHelpCommand command, uint? terminalWidth);
}

public sealed class RuntimeHelpRenderer : IHelpRenderer
{
    // No ANSI bold — plain "# " + uppercase only for this PR. See Azure/CLIPS#899 for the
    // follow-up on real bold rendering (color-policy gating, fixture/test impact).
    private static string SectionHeader(string title) => $"# {title.ToUpperInvariant()}";

    public string[] RenderRoot(IEnumerable<RuntimeHelpGroup> groups, uint? terminalWidth)
    {
        var lines = new List<string>
        {
            SectionHeader("Groups")
        };

        var ordered = groups.OrderBy(static g => g.GroupPath, StringComparer.OrdinalIgnoreCase).ToArray();
        for (var i = 0; i < ordered.Length; i++)
        {
            if (i > 0)
            {
                lines.Add(string.Empty);
            }
            AppendNameAndSummary(lines, ordered[i].GroupPath, ordered[i].Summary);
        }

        return [.. lines];
    }

    public string[] RenderGroup(RuntimeHelpGroup group, uint? terminalWidth)
    {
        var lines = new List<string>
        {
            SectionHeader("Group"),
        };
        AppendNameAndSummary(lines, $"az {group.GroupPath}", group.Summary);
        lines.Add(string.Empty);

        if (group.Subgroups.Count > 0)
        {
            lines.Add(SectionHeader("Subgroups"));
            for (var i = 0; i < group.Subgroups.Count; i++)
            {
                if (i > 0)
                {
                    lines.Add(string.Empty);
                }
                var subgroup = group.Subgroups[i];
                AppendNameAndSummary(lines, subgroup.Name, subgroup.Summary);
            }
            lines.Add(string.Empty);
        }

        lines.Add(SectionHeader("Commands"));

        if (group.Commands.Count == 0)
        {
            lines.Add("(none)");
        }
        else
        {
            for (var i = 0; i < group.Commands.Count; i++)
            {
                if (i > 0)
                {
                    lines.Add(string.Empty);
                }
                var command = group.Commands[i];
                var tags = command.Tags.Length == 0
                    ? string.Empty
                    : $" [{string.Join(",", command.Tags)}]";
                AppendNameAndSummary(lines, $"{command.Name}{tags}", command.Summary);
            }
        }

        AppendGlobalArguments(lines);

        return [.. lines];
    }

    public string[] RenderCommand(RuntimeHelpCommand command, uint? terminalWidth)
    {
        var lines = new List<string>
        {
            SectionHeader("Command"),
        };
        AppendNameAndSummary(lines, $"az {command.CommandPath}", command.Summary);
        lines.Add(string.Empty);

        AppendParameterSections(lines, command.Parameters);

        if (command.Examples.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add(SectionHeader("Examples"));
            for (var i = 0; i < command.Examples.Count; i++)
            {
                if (i > 0)
                {
                    lines.Add(string.Empty);
                }
                var example = command.Examples[i];
                lines.Add($"{i + 1}. {example.Title}");
                lines.Add(example.Command);
            }
        }

        AppendGlobalArguments(lines);

        return [.. lines];
    }

    // Stacks a "name:" line followed by its summary on the next line, matching the argument
    // stacking style. When summary is blank, only the name line is emitted (no trailing colon).
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

    private static void AppendGlobalArguments(List<string> lines)
    {
        lines.Add(string.Empty);
        lines.Add(SectionHeader("Global Arguments"));
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
    }

    // Emits an "Arguments" section for ungrouped params, then a section per named GroupName in
    // alphabetical order. Sections with no params are skipped. When Parameters is empty, the
    // single "Arguments" header + "(none)" placeholder is emitted (preserves prior behavior for
    // no-param commands like `az logout`).
    private static void AppendParameterSections(List<string> lines, IReadOnlyList<RuntimeHelpParameter> parameters)
    {
        if (parameters.Count == 0)
        {
            lines.Add(SectionHeader("Arguments"));
            lines.Add("(none)");
            return;
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

        var wroteSection = false;
        if (defaultBucket.Count > 0)
        {
            AppendParameterBucket(lines, "Arguments", defaultBucket, leadingBlank: false);
            wroteSection = true;
        }

        foreach (var (groupName, bucket) in groupedBuckets)
        {
            AppendParameterBucket(lines, groupName, bucket, leadingBlank: wroteSection);
            wroteSection = true;
        }
    }

    private static void AppendParameterBucket(List<string> lines, string header, IReadOnlyList<RuntimeHelpParameter> bucket, bool leadingBlank)
    {
        if (leadingBlank)
        {
            lines.Add(string.Empty);
        }
        lines.Add(SectionHeader(header));
        for (var i = 0; i < bucket.Count; i++)
        {
            if (i > 0)
            {
                lines.Add(string.Empty);
            }

            var parameter = bucket[i];
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
    }
}