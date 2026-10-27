// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Cli.Help;

/// <summary>
/// Centralised, repo-relative paths for the help system. Single source of truth for casing
/// (Windows is case-insensitive but Linux CI silently finds zero files when casing drifts;
/// pinned by tests/Cli.Tests/HelpPathsCasingTests).
/// </summary>
public static class HelpPaths
{
    /// <summary>Authored CLI markdown root, relative to <c>repoRoot</c>. <c>Help</c>/<c>Docs</c> are case-sensitive on Linux.</summary>
    public static readonly string[] CliHelpDocsSegments = ExtensionDiscoveryPaths.CliHelpDocsSegments;

    /// <summary>PowerShell modules root. Per-module markdown at <c>src/PS/&lt;Module&gt;/help/*.md</c> (lowercase).</summary>
    public static readonly string[] PsModulesSegments = ["src", "PS"];

    /// <summary>Lowercase folder name under each PS module that holds authored markdown.</summary>
    public const string PsHelpFolder = "help";

    /// <summary>Repo-rooted CLI help docs path.</summary>
    public static string CliHelpDocsRoot(string repoRoot)
        => Path.Combine([repoRoot, .. CliHelpDocsSegments]);

    /// <summary>Repo-rooted CLI help path for a verb folder.</summary>
    public static string CliVerbHelpDir(string repoRoot, string verbFolder)
        => Path.Combine([repoRoot, .. CliHelpDocsSegments, verbFolder]);

    /// <summary>Repo-rooted PS modules path.</summary>
    public static string PsModulesRoot(string repoRoot)
        => Path.Combine([repoRoot, .. PsModulesSegments]);
}
