// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Help;

namespace Azclips.Cli.Export;

/// <summary>
/// Projects our internal <see cref="HelpManifest"/> shape into CLI2Yaml's
/// <see cref="HelpFile"/> stream. Pure data mapping — no I/O, no reflection.
/// </summary>
/// <remarks>
/// Emission order per manifest: root group, then subgroups (one entry per intermediate
/// prefix implied by command paths, matching azure-cli), then one command entry per leaf.
/// Subgroup summaries come from an optional caller-supplied lookup; missing entries
/// render as stubs. <c>command_source</c> is always <c>null</c> (all-core model per
/// Q3 in the design plan).
/// </remarks>
internal static class HelpFileMapper
{
    // CLI2Yaml's Transformer.Run assumes a top-level "az" group entry keyed by
    // command == "" ("_root_.json"). Without it CLI2Yaml throws
    // KeyNotFoundException('az'). Synthesised here (no root markdown in our corpus today);
    // if we ever author src/CLI/Help/Docs/index.md, read its summary here.
    public static HelpFile CreateRootGroup(HelpTag productMaturity = ProductReleaseInfo.Maturity) => new(
        Command: "",
        Type: "group",
        ShortSummary: "AzClips CLI.",
        LongSummary: "",
        CommandSource: null,
        PreviewInfo: MapMaturity([], productMaturity).PreviewInfo,
        ExperimentalInfo: MapMaturity([], productMaturity).ExperimentalInfo,
        DeprecateInfo: null,
        BreakingChangeInfo: null,
        Examples: null,
        Parameters: null);

    public static IEnumerable<HelpFile> Map(
        HelpManifest manifest,
        Func<string, string>? subgroupSummary = null,
        HelpTag productMaturity = ProductReleaseInfo.Maturity)
    {
        var lookup = subgroupSummary ?? (static _ => string.Empty);
        var groupMaturity = MapMaturity([], productMaturity);

        yield return new HelpFile(
            Command: manifest.Verb,
            Type: "group",
            ShortSummary: manifest.RootSummary,
            LongSummary: "",
            CommandSource: null,
            PreviewInfo: groupMaturity.PreviewInfo,
            ExperimentalInfo: groupMaturity.ExperimentalInfo,
            DeprecateInfo: null,
            BreakingChangeInfo: null,
            Examples: null,
            Parameters: null);

        // Emit an entry for every intermediate subgroup implied by command paths, matching
        // azure-cli's behaviour. Summary comes from the caller-supplied lookup (typically
        // reads <sub>/index.md); missing entries render as stubs so downstream docfx still
        // has a group page to link into.
        foreach (var subgroupPath in EnumerateSubgroupPaths(manifest.Commands))
        {
            yield return new HelpFile(
                Command: subgroupPath,
                Type: "group",
                ShortSummary: lookup(subgroupPath),
                LongSummary: "",
                CommandSource: null,
                PreviewInfo: groupMaturity.PreviewInfo,
                ExperimentalInfo: groupMaturity.ExperimentalInfo,
                DeprecateInfo: null,
                BreakingChangeInfo: null,
                Examples: null,
                Parameters: null);
        }

        foreach (var command in manifest.Commands)
        {
            yield return MapCommand(command, productMaturity);
        }
    }

    private static IEnumerable<string> EnumerateSubgroupPaths(IEnumerable<HelpCommand> commands)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<string>();

        foreach (var command in commands)
        {
            var tokens = command.CliName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // Skip length 1 (that's the verb root, already emitted) and length == tokens.Length
            // (that's the leaf command itself). Everything in between is a subgroup.
            for (var len = 2; len < tokens.Length; len++)
            {
                var path = string.Join(' ', tokens.Take(len));
                if (seen.Add(path))
                {
                    ordered.Add(path);
                }
            }
        }

        return ordered
            .OrderBy(static path => path.Count(c => c == ' '))
            .ThenBy(static path => path, StringComparer.Ordinal);
    }

    private static HelpFile MapCommand(HelpCommand command, HelpTag productMaturity)
    {
        var parameters = command.Parameters
            .Select(MapParameter)
            .Concat(GlobalArguments.All)
            .ToList();

        var examples = command.Examples.Cli
            .Select(example => new HelpFileExample(ShortSummary: example.Title, Command: example.Command))
            .ToList();

        var (previewInfo, experimentalInfo) = MapMaturity(
            command.Tags, productMaturity, command.PreviewMessage, command.ExperimentalMessage);
        var (deprecateInfo, breakingChangeInfo) = MapDeprecated(command);

        return new HelpFile(
            Command: command.CliName,
            Type: "command",
            ShortSummary: command.Summary,
            LongSummary: command.Description,
            CommandSource: null,
            PreviewInfo: previewInfo,
            ExperimentalInfo: experimentalInfo,
            DeprecateInfo: deprecateInfo,
            BreakingChangeInfo: breakingChangeInfo,
            Examples: examples,
            Parameters: parameters);
    }

    private static HelpFileParameter MapParameter(HelpParameter parameter)
    {
        var choices = parameter.AllowedValues.Length > 0
            ? string.Join(", ", parameter.AllowedValues)
            : null;

        return new HelpFileParameter(
            Name: FormatParameterNameWithAliases(parameter),
            Required: parameter.Required,
            ShortSummary: parameter.Description,
            LongSummary: "",
            GroupName: parameter.GroupName,
            ValueSources: parameter.ValueSources,
            Default: null,
            Choices: choices,
            PreviewInfo: null,
            ExperimentalInfo: null,
            DeprecateInfo: null,
            BreakingChangeInfo: null);
    }

    private static string FormatParameterNameWithAliases(HelpParameter parameter)
    {
        var flags = new[] { $"--{parameter.Name}" }
            .Concat(parameter.Aliases.Select(alias => $"-{alias}"));
        return string.Join(" ", flags);
    }

    private static (string? PreviewInfo, string? ExperimentalInfo) MapMaturity(
        HelpTag[] tags,
        HelpTag productMaturity,
        string? previewMessage = null,
        string? experimentalMessage = null)
    {
        if (productMaturity is not (HelpTag.Preview or HelpTag.Experimental or HelpTag.GA))
        {
            throw new ArgumentException("Product maturity must be Preview, Experimental or GA.", nameof(productMaturity));
        }

        var maturityTags = tags.Where(tag => tag is HelpTag.Preview or HelpTag.Experimental or HelpTag.GA)
            .Distinct().ToArray();
        if (maturityTags.Length > 1)
        {
            throw new ArgumentException("Command has conflicting maturity tags.", nameof(tags));
        }

        var maturity = maturityTags.Length == 0 ? productMaturity : maturityTags[0];
        return maturity switch
        {
            HelpTag.Preview => (previewMessage ?? "", null),
            HelpTag.Experimental => (null, experimentalMessage ?? ""),
            _ => (null, null)
        };
    }

    private static (string? DeprecateInfo, string? BreakingChangeInfo) MapDeprecated(HelpCommand command)
    {
        if (!command.Tags.Contains(HelpTag.Deprecated))
        {
            return (null, null);
        }

        var message = command.DeprecatedMessage ?? "";
        // Loop convention: [Breaking Change] sentinel inside the message routes the
        // same string to both fields. Splitting policy TBD after CLI2Yaml smoke test.
        var breakingChange = message.Contains("[Breaking Change]", StringComparison.OrdinalIgnoreCase)
            ? message
            : null;
        return (message, breakingChange);
    }
}
