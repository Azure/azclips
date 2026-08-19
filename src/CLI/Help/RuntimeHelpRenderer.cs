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
            $"    az {group.GroupPath} : {group.Summary}",
            string.Empty
        };

        if (group.Subgroups.Count > 0)
        {
            lines.Add("Subgroups:");
            foreach (var subgroup in group.Subgroups)
            {
                var subgroupSummary = string.IsNullOrWhiteSpace(subgroup.Summary)
                    ? string.Empty
                    : $" : {subgroup.Summary}";
                lines.Add($"    {subgroup.Name}{subgroupSummary}");
            }
            lines.Add(string.Empty);
        }

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

        AppendGlobalArguments(lines);

        return [.. lines];
    }

    public string[] RenderCommand(RuntimeHelpCommand command, uint? terminalWidth)
    {
        var lines = new List<string>
        {
            "Command",
            $"    az {command.CommandPath} : {command.Summary}",
            string.Empty,
        };

        AppendParameterSections(lines, command.Parameters);

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

        AppendGlobalArguments(lines);

        return [.. lines];
    }

    private static void AppendGlobalArguments(List<string> lines)
    {
        lines.Add(string.Empty);
        lines.Add("Global Arguments");
        foreach (var flag in KnownGlobalFlags.All)
        {
            var aliases = flag.Aliases.Count == 0
                ? string.Empty
                : " " + string.Join(" ", flag.Aliases);
            lines.Add($"    {flag.Name}{aliases} : {flag.Summary}");
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
            lines.Add("Arguments");
            lines.Add("    (none)");
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
        lines.Add(header);
        foreach (var parameter in bucket)
        {
            var aliases = parameter.Aliases.Length == 0
                ? string.Empty
                : $" -{string.Join(" -", parameter.Aliases)}";
            var required = parameter.Required ? " [Required]" : string.Empty;
            lines.Add($"    --{parameter.Name}{aliases}{required} : {parameter.Description}");

            if (!string.IsNullOrEmpty(parameter.ValueSources))
            {
                var suffix = parameter.ValueSources.EndsWith('.') ? string.Empty : ".";
                lines.Add($"        Values from: {parameter.ValueSources}{suffix}");
            }
        }
    }
}