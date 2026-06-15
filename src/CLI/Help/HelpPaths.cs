// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Cli.Help;

/// <summary>
/// Centralised, repo-relative paths for the help system. These constants are the
/// single source of truth for casing — Windows is case-insensitive, but Linux CI
/// silently finds zero files when the casing drifts (we have a path-casing test
/// that pins this in tests/Cli.Tests). Do not hand-construct the paths.
/// </summary>
public static class HelpPaths
{
    /// <summary>
    /// Authored CLI markdown root, relative to <c>repoRoot</c>.
    /// Capital <c>Help</c>; capital-D <c>Docs</c> — both are case-sensitive on Linux.
    /// </summary>
    public static readonly string[] CliHelpDocsSegments = ["src", "CLI", "Help", "Docs"];

    /// <summary>
    /// PowerShell help authoring root, relative to <c>repoRoot</c>. Per-module
    /// markdown lives at <c>src/PS/&lt;Module&gt;/help/*.md</c> (lowercase <c>help</c>).
    /// </summary>
    public static readonly string[] PsModulesSegments = ["src", "PS"];

    /// <summary>The folder name (lowercase) under each PS module that holds authored markdown.</summary>
    public const string PsHelpFolder = "help";

    /// <summary>Joins <paramref name="repoRoot"/> with <see cref="CliHelpDocsSegments"/>.</summary>
    public static string CliHelpDocsRoot(string repoRoot)
        => Path.Combine([repoRoot, .. CliHelpDocsSegments]);

    /// <summary>Joins <paramref name="repoRoot"/> with <see cref="CliHelpDocsSegments"/> + verb folder.</summary>
    public static string CliVerbHelpDir(string repoRoot, string verbFolder)
        => Path.Combine([repoRoot, .. CliHelpDocsSegments, verbFolder]);

    /// <summary>Joins <paramref name="repoRoot"/> with <see cref="PsModulesSegments"/>.</summary>
    public static string PsModulesRoot(string repoRoot)
        => Path.Combine([repoRoot, .. PsModulesSegments]);
}
