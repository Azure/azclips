// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions;

/// <summary>
/// Provides lightweight help metadata for CLI rendering without loading command execution code.
/// </summary>
public interface IExtensionHelpProvider
{
    /// <summary>
    /// Gets the extension name that owns this help provider.
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