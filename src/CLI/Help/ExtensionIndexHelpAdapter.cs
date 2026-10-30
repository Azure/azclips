// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using System.Reflection;

namespace Core.Cli.Help;

/// <summary>
/// Verb-scoped projection of a discovery-produced CLI command table + the markdown help corpus
/// into the runtime help tree. One instance covers exactly one CLI verb
/// (e.g. <c>"vm"</c>); multi-verb extensions create one adapter per verb.
/// </summary>
/// <remarks>
/// <para>
/// Resolution is markdown-first with attribute fallback
/// (<c>[ProcessorInfo]</c> / <c>[ArgumentInfo]</c>). Examples are filtered to CLI-only —
/// PowerShell examples flow through <see cref="ExtensionHelpManifestBuilder"/>.
/// </para>
/// <para>
/// Markdown lookup uses <c>(verb, relativeCommandPath)</c>, e.g.
/// <c>("vm", "create")</c> → <c>src/CLI/Help/Docs/vm/create.md</c>;
/// <c>("login", "")</c>     → <c>src/CLI/Help/Docs/login/index.md</c>.
/// </para>
/// </remarks>
public sealed class ExtensionIndexHelpAdapter : IExtensionHelpProvider
{
    /// <summary>Command-table entries for <see cref="Verb"/>, keyed by path relative to the verb.</summary>
    /// <remarks>
    /// Extensions key <c>s_cliTable</c> on the dispatch surface (full <c>"verb path"</c>) so the
    /// runtime engine can look up <c>Invocation.CommandName</c> verbatim. This adapter strips
    /// the verb prefix once at construction so all downstream help logic works in the
    /// per-verb namespace, e.g. <c>""</c> for the verb itself and <c>"create"</c> for
    /// <c>vm create</c>. Entries belonging to other verbs are dropped.
    /// </remarks>
    private readonly IReadOnlyDictionary<string, CommandMetadata> _verbCommands;

    public ExtensionIndexHelpAdapter(IReadOnlyDictionary<string, CommandMetadata> commandTable, string verb)
    {
        ArgumentNullException.ThrowIfNull(commandTable);
        Verb = string.IsNullOrWhiteSpace(verb)
            ? throw new ArgumentException("Verb cannot be null or empty.", nameof(verb))
            : verb.ToLowerInvariant();

        _verbCommands = BuildVerbScopedTable(commandTable, Verb);
    }

    public string Verb { get; }

    public RuntimeHelpGroup GetRootGroup()
        => BuildGroup([Verb], MarkdownCommandHelpProvider.Get(Verb, string.Empty).Summary)!;

    public RuntimeHelpGroup? GetGroupHelp(IReadOnlyList<string> groupPath)
    {
        if (!IsUnderVerb(groupPath))
        {
            return null;
        }

        var relativeKey = GetRelativeKey(groupPath);
        var summary = MarkdownCommandHelpProvider.Get(Verb, relativeKey).Summary;

        return BuildGroup(groupPath, summary, returnNullWhenEmpty: groupPath.Count > 1);
    }

    public RuntimeHelpCommand? GetCommandHelp(IReadOnlyList<string> commandPath)
    {
        if (!IsUnderVerb(commandPath))
        {
            return null;
        }

        var relativeKey = GetRelativeKey(commandPath);
        if (!_verbCommands.TryGetValue(relativeKey, out var metadata))
        {
            return null;
        }

        var processorType = metadata.ProcessorType;
        var parameters = metadata.Parameters
            .Select(kvp =>
            {
                var property = ProcessorParameterReflection.FindPropertyForParameter(processorType, kvp.Value.Name);
                return new RuntimeHelpParameter(
                    Name: kvp.Key,
                    Aliases: kvp.Value.Aliases,
                    Required: kvp.Value.IsRequired,
                    Description: kvp.Value.Description,
                    Summary: kvp.Value.Description,
                    AllowedValues: kvp.Value.AllowedValues,
                    ParameterSetName: "(All)",
                    SupportsPipeline: false,
                    SupportsWildcards: false,
                    GroupName: ProcessorParameterReflection.GetGroupName(property),
                    ValueSources: ProcessorParameterReflection.GetValueSources(property));
            })
            .ToList();

        var markdown = MarkdownCommandHelpProvider.Get(Verb, relativeKey);
        var summary = GetSummary(metadata, markdown);
        var description = GetDescription(metadata, markdown);
        var examples = GetExamples(metadata, markdown);

        return new RuntimeHelpCommand(
            CommandPath: string.Join(" ", commandPath.Select(static segment => segment.ToLowerInvariant())),
            Summary: summary,
            Description: description,
            Parameters: parameters,
            Examples: examples,
            Tags: GetTags(metadata));
    }

    private RuntimeHelpGroup? BuildGroup(IReadOnlyList<string> groupPath, string summary, bool returnNullWhenEmpty = false)
    {
        var relativePrefix = GetRelativeKey(groupPath);
        var subgroupSummaries = new Dictionary<string, RuntimeHelpGroupSummary>(StringComparer.OrdinalIgnoreCase);
        var commandSummaries = new List<RuntimeHelpCommandSummary>();

        // A verb that also has a runnable root command (e.g. "az login" itself) shows up
        // as the empty-key entry in the per-verb table.
        if (string.IsNullOrEmpty(relativePrefix) && _verbCommands.TryGetValue(string.Empty, out var rootCommandMetadata))
        {
            commandSummaries.Add(new RuntimeHelpCommandSummary(
                Name: Verb,
                Summary: GetSummary(rootCommandMetadata, MarkdownCommandHelpProvider.Get(Verb, string.Empty)),
                Tags: GetTags(rootCommandMetadata)));
        }

        foreach (var kvp in _verbCommands.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
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
                    Summary: GetSummary(kvp.Value, MarkdownCommandHelpProvider.Get(Verb, kvp.Key)),
                    Tags: GetTags(kvp.Value)));
                continue;
            }

            var subgroupName = segments[0];
            if (!subgroupSummaries.ContainsKey(subgroupName))
            {
                var subgroupKey = string.IsNullOrEmpty(relativePrefix)
                    ? subgroupName
                    : relativePrefix + " " + subgroupName;
                var subgroupSummary = MarkdownCommandHelpProvider.Get(Verb, subgroupKey).Summary;
                subgroupSummaries[subgroupName] = new RuntimeHelpGroupSummary(subgroupName, subgroupSummary);
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

    private bool IsUnderVerb(IReadOnlyList<string> path)
        => path.Count > 0 && path[0].Equals(Verb, StringComparison.OrdinalIgnoreCase);

    private static string GetRelativeKey(IReadOnlyList<string> path)
    {
        // path[0] is the verb; everything after is the per-verb command key.
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

    /// <summary>
    /// Scopes the full-path CLI table down to one verb and strips the verb prefix from
    /// each key. Entries from other verbs (e.g. <c>"disk list"</c> when scoping to
    /// <c>"vm"</c>) are dropped.
    /// </summary>
    private static IReadOnlyDictionary<string, CommandMetadata> BuildVerbScopedTable(
        IReadOnlyDictionary<string, CommandMetadata> fullTable,
        string verb)
    {
        var result = new Dictionary<string, CommandMetadata>(StringComparer.OrdinalIgnoreCase);
        var prefix = verb + " ";

        foreach (var kvp in fullTable)
        {
            var key = kvp.Key ?? string.Empty;
            if (key.Equals(verb, StringComparison.OrdinalIgnoreCase))
            {
                result[string.Empty] = kvp.Value;
            }
            else if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                result[key[prefix.Length..]] = kvp.Value;
            }
        }

        return result;
    }

    private static ProcessorInfoAttribute? GetProcessorInfo(CommandMetadata metadata)
        => metadata.ProcessorType.GetCustomAttribute<ProcessorInfoAttribute>();

    private static HelpTag[] GetTags(CommandMetadata metadata)
        => GetProcessorInfo(metadata)?.Tags ?? [];

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
