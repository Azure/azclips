using Core.Abstractions;
using Core.Cli.Parser;
using Core.Runtime;
using System.Reflection;

namespace Core.Cli.Help;

/// <summary>
/// Bridges an <see cref="IExtensionIndex"/> + its markdown help corpus into the runtime
/// help tree (<see cref="RuntimeHelpGroup"/> / <see cref="RuntimeHelpCommand"/>) consumed
/// by the CLI help renderer.
/// </summary>
/// <remarks>
/// <para>
/// Resolution order for summary / description / examples is markdown-first, attribute
/// fallback: <see cref="MarkdownCommandHelpProvider"/> is queried first; if the markdown
/// field is empty, <c>[ProcessorInfo]</c> / <c>[ArgumentInfo]</c> attributes on the
/// processor type are used; finally a humanized command name is the last resort.
/// </para>
/// <para>
/// Surface filtering for CLI: examples with <see cref="MarkdownExample.IsPowerShell"/>
/// are filtered out before being returned. The PowerShell surface (MAML generation) goes
/// through <see cref="ExtensionHelpManifestBuilder"/> instead and receives the full list.
/// </para>
/// </remarks>
public sealed class ExtensionIndexHelpAdapter : IExtensionHelpProvider
{
    private readonly IExtensionIndex _extensionIndex;
    private readonly IReadOnlyDictionary<string, CommandMetadata> _cliTable;
    private readonly string _rootGroupName;
    private readonly string _rootGroupPath;
    private readonly string _extensionHelpFolder;

    public ExtensionIndexHelpAdapter(IExtensionIndex extensionIndex, string extensionName)
    {
        _extensionIndex = extensionIndex ?? throw new ArgumentNullException(nameof(extensionIndex));
        ExtensionName = string.IsNullOrWhiteSpace(extensionName)
            ? throw new ArgumentException("Extension name cannot be null or empty.", nameof(extensionName))
            : extensionName;

        _cliTable = _extensionIndex.GetCommandTable(ClientType.CLI);
        _rootGroupName = CommandGroupIndex.GetCommandGroupNameByExtensionName(extensionName);
        _rootGroupPath = _rootGroupName.ToLowerInvariant();
        _extensionHelpFolder = extensionName;
    }

    public string ExtensionName { get; }

    /// <summary>
    /// Returns the top-level groups exposed by this extension. Today this is a single
    /// group keyed on the extension's CLI group name; nested groups are discovered on
    /// demand via <see cref="GetGroupHelp"/>.
    /// </summary>
    public IReadOnlyList<RuntimeHelpGroup> GetRootGroups()
        => [BuildGroup([_rootGroupPath], _extensionIndex.CliRootSummary)!];

    /// <summary>
    /// Resolves group-level help for the supplied command path (e.g. <c>["group"]</c> or
    /// <c>["group", "role"]</c>). Returns <c>null</c> when the path doesn't fall under
    /// this extension's root group, or when a nested group resolves to no subgroups and
    /// no commands.
    /// </summary>
    public RuntimeHelpGroup? GetGroupHelp(IReadOnlyList<string> groupPath)
    {
        if (!IsUnderRootGroup(groupPath))
        {
            return null;
        }

        var summary = groupPath.Count == 1
            ? _extensionIndex.CliRootSummary
            : string.Empty;

        return BuildGroup(groupPath, summary, returnNullWhenEmpty: groupPath.Count > 1);
    }

    /// <summary>
    /// Resolves command-level help for the supplied path (e.g. <c>["group", "create"]</c>).
    /// Returns <c>null</c> when the path doesn't resolve to a routed command in this
    /// extension's CLI table. Examples are filtered to CLI-only
    /// (<see cref="MarkdownExample.IsPowerShell"/> = <c>false</c>).
    /// </summary>
    public RuntimeHelpCommand? GetCommandHelp(IReadOnlyList<string> commandPath)
    {
        if (!IsUnderRootGroup(commandPath))
        {
            return null;
        }

        var relativeKey = GetRelativeKey(commandPath);
        if (!_cliTable.TryGetValue(relativeKey, out var metadata))
        {
            return null;
        }

        var parameters = metadata.Parameters
            .Select(static kvp => new RuntimeHelpParameter(
                Name: kvp.Key,
                Aliases: kvp.Value.Aliases,
                Required: kvp.Value.IsRequired,
                Description: kvp.Value.Description,
                Summary: kvp.Value.Description,
                AllowedValues: kvp.Value.AllowedValues,
                ParameterSetName: "(All)",
                SupportsPipeline: false,
                SupportsWildcards: false))
            .ToList();

        var markdown = MarkdownCommandHelpProvider.Get(_extensionHelpFolder, relativeKey);
        var summary = GetSummary(metadata, markdown);
        var description = GetDescription(metadata, markdown);
        var examples = GetExamples(metadata, markdown);

        return new RuntimeHelpCommand(
            CommandPath: string.Join(" ", commandPath.Select(static segment => segment.ToLowerInvariant())),
            Summary: summary,
            Description: description,
            Parameters: parameters,
            Examples: examples,
            Tags: []);
    }

    private RuntimeHelpGroup? BuildGroup(IReadOnlyList<string> groupPath, string summary, bool returnNullWhenEmpty = false)
    {
        var relativePrefix = GetRelativeKey(groupPath);
        var subgroupSummaries = new Dictionary<string, RuntimeHelpGroupSummary>(StringComparer.OrdinalIgnoreCase);
        var commandSummaries = new List<RuntimeHelpCommandSummary>();

        if (string.IsNullOrEmpty(relativePrefix) && _cliTable.TryGetValue(string.Empty, out var rootCommandMetadata))
        {
            commandSummaries.Add(new RuntimeHelpCommandSummary(
                Name: _rootGroupPath,
                Summary: GetSummary(rootCommandMetadata, MarkdownCommandHelpProvider.Get(_extensionHelpFolder, string.Empty)),
                Tags: []));
        }

        foreach (var kvp in _cliTable.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrEmpty(kvp.Key))
            {
                continue;
            }

            var remainder = GetRemainingPath(kvp.Key, relativePrefix);
            if (remainder == null)
            {
                continue;
            }

            var segments = remainder.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                continue;
            }

            if (segments.Length == 1)
            {
                commandSummaries.Add(new RuntimeHelpCommandSummary(
                    Name: segments[0],
                    Summary: GetSummary(kvp.Value, MarkdownCommandHelpProvider.Get(_extensionHelpFolder, kvp.Key)),
                    Tags: []));
                continue;
            }

            var subgroupName = segments[0];
            if (!subgroupSummaries.ContainsKey(subgroupName))
            {
                subgroupSummaries[subgroupName] = new RuntimeHelpGroupSummary(subgroupName, string.Empty);
            }
        }

        if (returnNullWhenEmpty && subgroupSummaries.Count == 0 && commandSummaries.Count == 0)
        {
            return null;
        }

        return new RuntimeHelpGroup(
            GroupPath: string.Join(" ", groupPath.Select(static segment => segment.ToLowerInvariant())),
            Summary: summary,
            Subgroups: subgroupSummaries.Values.OrderBy(static group => group.Name, StringComparer.Ordinal).ToList(),
            Commands: commandSummaries);
    }

    private bool IsUnderRootGroup(IReadOnlyList<string> path)
    {
        return path.Count > 0 && path[0].Equals(_rootGroupPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetRelativeKey(IReadOnlyList<string> path)
    {
        return path.Count <= 1
            ? string.Empty
            : string.Join(" ", path.Skip(1).Select(static segment => segment.ToLowerInvariant()));
    }

    private static string? GetRemainingPath(string key, string relativePrefix)
    {
        if (string.IsNullOrEmpty(relativePrefix))
        {
            return key;
        }

        if (key.Equals(relativePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var prefix = relativePrefix + " ";
        return key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? key[prefix.Length..]
            : null;
    }

    private static ProcessorInfoAttribute? GetProcessorInfo(CommandMetadata metadata)
        => metadata.ProcessorType.GetCustomAttribute<ProcessorInfoAttribute>();

    private static string GetSummary(CommandMetadata metadata, MarkdownCommandHelp markdown)
    {
        if (!string.IsNullOrWhiteSpace(markdown.Summary))
        {
            return markdown.Summary;
        }

        var processorInfo = GetProcessorInfo(metadata);
        if (!string.IsNullOrWhiteSpace(processorInfo?.Summary))
        {
            return processorInfo.Summary.Trim();
        }

        if (!string.IsNullOrWhiteSpace(processorInfo?.Description))
        {
            return processorInfo.Description.Trim();
        }

        return HumanizeCommandName(metadata.Name);
    }

    private static string GetDescription(CommandMetadata metadata, MarkdownCommandHelp markdown)
    {
        if (!string.IsNullOrWhiteSpace(markdown.Description))
        {
            return markdown.Description;
        }

        var processorInfo = GetProcessorInfo(metadata);
        if (!string.IsNullOrWhiteSpace(processorInfo?.Description))
        {
            return processorInfo.Description.Trim();
        }

        return HumanizeCommandName(metadata.Name);
    }

    private static List<RuntimeHelpExample> GetExamples(CommandMetadata metadata, MarkdownCommandHelp markdown)
    {
        if (markdown.Examples.Count > 0)
        {
            return markdown.Examples
                .Where(static example => !example.IsPowerShell)
                .Select(static example => new RuntimeHelpExample(example.Title, example.Command))
                .ToList();
        }

        var processorInfo = GetProcessorInfo(metadata);
        if (processorInfo?.Examples == null || processorInfo.Examples.Length == 0)
        {
            return [];
        }

        var result = new List<RuntimeHelpExample>();
        for (var i = 0; i < processorInfo.Examples.Length; i++)
        {
            var raw = processorInfo.Examples[i]?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var delimiter = raw.IndexOf('|');
            if (delimiter <= 0 || delimiter >= raw.Length - 1)
            {
                result.Add(new RuntimeHelpExample($"Example {i + 1}", raw));
                continue;
            }

            var title = raw[..delimiter].Trim();
            var command = raw[(delimiter + 1)..].Trim();
            result.Add(new RuntimeHelpExample(
                string.IsNullOrWhiteSpace(title) ? $"Example {i + 1}" : title,
                command));
        }

        return result;
    }

    private static string HumanizeCommandName(string name)
    {
        return name.EndsWith("Command", StringComparison.Ordinal)
            ? name[..^"Command".Length]
            : name;
    }
}