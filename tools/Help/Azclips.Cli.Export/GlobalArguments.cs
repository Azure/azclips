// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azclips.Cli.Export;

/// <summary>
/// Hardcoded global argument set injected into every command's parameter list per CLI2Yaml
/// contract D1: docs.ms identifies globals by <c>group_name == "Global Arguments"</c>.
/// </summary>
/// <remarks>
/// Summaries and defaults are cribbed verbatim from a rendered azure-cli reference page
/// (e.g. <c>az group create</c> on <c>learn.microsoft.com/cli/azure/group?view=azure-cli-latest</c>).
/// Re-sync from the same source if azure-cli updates them.
/// </remarks>
internal static class GlobalArguments
{
    private const string GroupName = "Global Arguments";

    public static readonly IReadOnlyList<HelpFileParameter> All =
    [
        new HelpFileParameter(
            Name: "--debug",
            Required: false,
            ShortSummary: "Increase logging verbosity to show all debug logs.",
            LongSummary: "",
            GroupName: GroupName,
            ValueSources: null,
            Default: null,
            Choices: null,
            PreviewInfo: null,
            ExperimentalInfo: null,
            DeprecateInfo: null,
            BreakingChangeInfo: null),
        new HelpFileParameter(
            Name: "--help -h",
            Required: false,
            ShortSummary: "Show this help message and exit.",
            LongSummary: "",
            GroupName: GroupName,
            ValueSources: null,
            Default: null,
            Choices: null,
            PreviewInfo: null,
            ExperimentalInfo: null,
            DeprecateInfo: null,
            BreakingChangeInfo: null),
        new HelpFileParameter(
            Name: "--only-show-errors",
            Required: false,
            ShortSummary: "Only show errors, suppressing warnings.",
            LongSummary: "",
            GroupName: GroupName,
            ValueSources: null,
            Default: null,
            Choices: null,
            PreviewInfo: null,
            ExperimentalInfo: null,
            DeprecateInfo: null,
            BreakingChangeInfo: null),
        new HelpFileParameter(
            Name: "--output -o",
            Required: false,
            ShortSummary: "Output format.",
            LongSummary: "",
            GroupName: GroupName,
            ValueSources: null,
            Default: "json",
            Choices: "json, jsonc, none, table, tsv, yaml, yamlc",
            PreviewInfo: null,
            ExperimentalInfo: null,
            DeprecateInfo: null,
            BreakingChangeInfo: null),
        new HelpFileParameter(
            Name: "--query",
            Required: false,
            ShortSummary: "JMESPath query string. See http://jmespath.org/ for more information and examples.",
            LongSummary: "",
            GroupName: GroupName,
            ValueSources: null,
            Default: null,
            Choices: null,
            PreviewInfo: null,
            ExperimentalInfo: null,
            DeprecateInfo: null,
            BreakingChangeInfo: null),
        new HelpFileParameter(
            Name: "--subscription",
            Required: false,
            ShortSummary: "Name or ID of subscription. You can configure the default subscription using `az account set -s NAME_OR_ID`.",
            LongSummary: "",
            GroupName: GroupName,
            ValueSources: null,
            Default: null,
            Choices: null,
            PreviewInfo: null,
            ExperimentalInfo: null,
            DeprecateInfo: null,
            BreakingChangeInfo: null),
        new HelpFileParameter(
            Name: "--verbose",
            Required: false,
            ShortSummary: "Increase logging verbosity. Use --debug for full debug logs.",
            LongSummary: "",
            GroupName: GroupName,
            ValueSources: null,
            Default: null,
            Choices: null,
            PreviewInfo: null,
            ExperimentalInfo: null,
            DeprecateInfo: null,
            BreakingChangeInfo: null),
    ];
}
