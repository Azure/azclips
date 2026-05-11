using Core.Abstractions;
using Core.Cli.Parser;

namespace Core.Cli.Help;

public sealed class ExtensionIndexHelpAdapter : IExtensionHelpProvider
{
    private readonly IExtensionIndex _extensionIndex;
    private readonly IReadOnlyDictionary<string, CommandMetadata> _cliTable;
    private readonly string _rootGroupName;
    private readonly string _rootGroupPath;

    public ExtensionIndexHelpAdapter(IExtensionIndex extensionIndex, string extensionName)
    {
        _extensionIndex = extensionIndex ?? throw new ArgumentNullException(nameof(extensionIndex));
        ExtensionName = string.IsNullOrWhiteSpace(extensionName)
            ? throw new ArgumentException("Extension name cannot be null or empty.", nameof(extensionName))
            : extensionName;

        _cliTable = _extensionIndex.GetCommandTable(ClientType.CLI);
        _rootGroupName = CommandGroupIndex.GetCommandGroupNameByExtensionName(extensionName);
        _rootGroupPath = _rootGroupName.ToLowerInvariant();
    }

    public string ExtensionName { get; }

    public IReadOnlyList<RuntimeHelpGroup> GetRootGroups()
        => [BuildGroup([_rootGroupPath], CommandGroupIndex.GetGroupSummary(_rootGroupName))!];

    public RuntimeHelpGroup? GetGroupHelp(IReadOnlyList<string> groupPath)
    {
        if (!IsUnderRootGroup(groupPath))
        {
            return null;
        }

        var summary = groupPath.Count == 1
            ? CommandGroupIndex.GetGroupSummary(_rootGroupName)
            : string.Empty;

        return BuildGroup(groupPath, summary, returnNullWhenEmpty: groupPath.Count > 1);
    }

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

        var examples = (metadata.Examples ?? [])
            .Select(static example => new RuntimeHelpExample(example.Title, example.Command))
            .ToList();

        return new RuntimeHelpCommand(
            CommandPath: string.Join(" ", commandPath.Select(static segment => segment.ToLowerInvariant())),
            Summary: metadata.Summary,
            Description: metadata.Description,
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
                Summary: rootCommandMetadata.Summary,
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
                    Summary: kvp.Value.Summary,
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
}