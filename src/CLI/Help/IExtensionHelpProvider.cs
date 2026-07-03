// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Cli.Help;

/// <summary>
/// Provides CLI-shaped runtime help metadata for one verb (e.g. <c>"vm"</c>) without
/// loading command execution code. A single extension may contribute multiple verbs;
/// each verb gets its own help provider instance. Typically implemented by an adapter
/// over <see cref="IExtensionIndex"/>.
/// </summary>
public interface IExtensionHelpProvider
{
    /// <summary>
    /// The CLI verb whose metadata is being projected (e.g. <c>"vm"</c>, <c>"disk"</c>, <c>"login"</c>).
    /// </summary>
    string Verb { get; }

    /// <summary>
    /// Returns the root help group for this verb. The root path is <c>[Verb]</c>.
    /// </summary>
    RuntimeHelpGroup GetRootGroup();

    /// <summary>
    /// Returns help for a specific CLI group path (e.g. <c>["vm"]</c>). Returns
    /// <c>null</c> when the path is outside this verb.
    /// </summary>
    RuntimeHelpGroup? GetGroupHelp(IReadOnlyList<string> groupPath);

    /// <summary>
    /// Returns help for a specific CLI command path (e.g. <c>["vm", "create"]</c>).
    /// Returns <c>null</c> when the path doesn't resolve under this verb.
    /// </summary>
    RuntimeHelpCommand? GetCommandHelp(IReadOnlyList<string> commandPath);
}