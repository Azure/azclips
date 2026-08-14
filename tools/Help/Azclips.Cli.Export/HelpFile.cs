// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azclips.Cli.Export;

/// <summary>
/// CLI2Yaml's per-entry JSON shape (one record per group or command). Field names below are
/// PascalCase; the exporter serializes with <see cref="System.Text.Json.JsonNamingPolicy.SnakeCaseLower"/>
/// so <c>ShortSummary</c> → <c>"short_summary"</c> etc., matching CLI2Yaml's
/// decompiled <c>Models/HelpFile.cs</c>.
/// </summary>
internal sealed record HelpFile(
    string Command,
    string Type,
    string ShortSummary,
    string LongSummary,
    string? CommandSource,
    string? PreviewInfo,
    string? ExperimentalInfo,
    string? DeprecateInfo,
    string? BreakingChangeInfo,
    IReadOnlyList<HelpFileExample>? Examples,
    IReadOnlyList<HelpFileParameter>? Parameters);

internal sealed record HelpFileExample(
    string ShortSummary,
    string Command);

internal sealed record HelpFileParameter(
    string Name,
    bool Required,
    string ShortSummary,
    string LongSummary,
    string? GroupName,
    string? ValueSources,
    string? Default,
    string? Choices,
    string? PreviewInfo,
    string? ExperimentalInfo,
    string? DeprecateInfo,
    string? BreakingChangeInfo);
