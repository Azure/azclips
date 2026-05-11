// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions;

/// <summary>
/// Provides CLI-shaped runtime help metadata for rendering without loading command execution code.
/// This is a CLI-side contract and is typically implemented by an adapter over <see cref="IExtensionIndex"/>,
/// not by each extension directly.
/// </summary>
public interface IExtensionHelpProvider
{
    /// <summary>
    /// Gets the extension name whose metadata is being projected.
    /// </summary>
    string ExtensionName { get; }

    /// <summary>
    /// Returns top-level CLI groups contributed by this extension.
    /// </summary>
    IReadOnlyList<RuntimeHelpGroup> GetRootGroups();

    /// <summary>
    /// Returns help for a specific CLI group path (for example: ["group"]).
    /// </summary>
    RuntimeHelpGroup? GetGroupHelp(IReadOnlyList<string> groupPath);

    /// <summary>
    /// Returns help for a specific CLI command path (for example: ["group", "create"]).
    /// </summary>
    RuntimeHelpCommand? GetCommandHelp(IReadOnlyList<string> commandPath);
}