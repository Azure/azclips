// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli.Parser;

namespace Azclips.Cli.Export;

/// <summary>
/// Global arguments injected into every command's parameter list per CLI2Yaml contract D1:
/// docs.ms identifies globals by <c>group_name == "Global Arguments"</c>. Projects
/// <see cref="KnownGlobalFlags.All"/> into <see cref="HelpFileParameter"/>s so the docs export
/// and runtime <c>--help</c> share one source of truth.
/// </summary>
internal static class GlobalArguments
{
    private const string GroupName = "Global Arguments";

    public static readonly IReadOnlyList<HelpFileParameter> All = KnownGlobalFlags.All
        .Select(flag => new HelpFileParameter(
            Name: flag.Aliases.Count == 0 ? flag.Name : $"{flag.Name} {string.Join(' ', flag.Aliases)}",
            Required: false,
            ShortSummary: flag.Summary,
            LongSummary: "",
            GroupName: GroupName,
            ValueSources: null,
            Default: flag.Default,
            Choices: flag.Choices,
            PreviewInfo: null,
            ExperimentalInfo: null,
            DeprecateInfo: null,
            BreakingChangeInfo: null))
        .ToList();
}
