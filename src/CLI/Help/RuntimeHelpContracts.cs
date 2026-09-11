// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Cli.Help;

/// <summary>
/// Represents a CLI group node used by runtime help rendering.
/// </summary>
public record RuntimeHelpGroup(
    string GroupPath,
    string Summary,
    IReadOnlyList<RuntimeHelpGroupSummary> Subgroups,
    IReadOnlyList<RuntimeHelpCommandSummary> Commands);

/// <summary>
/// Represents a command summary row displayed in group help.
/// </summary>
public record RuntimeHelpCommandSummary(
    string Name,
    string Summary,
    HelpTag[] Tags);

/// <summary>
/// Represents a subgroup summary row displayed in group help.
/// </summary>
public record RuntimeHelpGroupSummary(
    string Name,
    string Summary);

/// <summary>
/// Represents detailed command help displayed for a CLI command.
/// </summary>
public record RuntimeHelpCommand(
    string CommandPath,
    string Summary,
    string Description,
    IReadOnlyList<RuntimeHelpParameter> Parameters,
    IReadOnlyList<RuntimeHelpExample> Examples,
    HelpTag[] Tags);

/// <summary>
/// Represents a parameter in runtime command help output.
/// </summary>
public record RuntimeHelpParameter(
    string Name,
    string[] Aliases,
    bool Required,
    string Description,
    string Summary,
    string[] AllowedValues,
    string ParameterSetName,
    bool SupportsPipeline,
    bool SupportsWildcards,
    string? GroupName = null,
    string? ValueSources = null);

/// <summary>
/// Represents a runnable example in runtime command help output.
/// </summary>
public record RuntimeHelpExample(
    string Title,
    string Command);