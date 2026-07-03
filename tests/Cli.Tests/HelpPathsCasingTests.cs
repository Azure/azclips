// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Cli.Help;

namespace Cli.Tests;

/// <summary>
/// Pin on-disk casing of help-system folders. Windows is case-insensitive, so drift
/// like <c>src/CLI/help/</c> compiles locally while Linux CI silently finds zero files.
/// </summary>
/// <remarks>Walks up to <c>azclips.slnx</c>; skipped if the test process can't find the repo root.</remarks>
public class HelpPathsCasingTests
{
    [Fact]
    public void CliHelpDocs_ShouldExistWithExactCasing_OnDisk()
    {
        var repoRoot = TryFindRepoRoot();
        if (repoRoot is null)
        {
            return; // not a dev tree
        }

        var helpDocs = HelpPaths.CliHelpDocsRoot(repoRoot);
        Assert.True(Directory.Exists(helpDocs), $"Expected help docs root at: {helpDocs}");

        // Path.Combine + Directory.Exists is case-insensitive on Windows; enumerate the parent and compare names.
        AssertExactSegmentCasing(repoRoot, HelpPaths.CliHelpDocsSegments);
    }

    [Fact]
    public void PsModulesRoot_ShouldExistWithExactCasing_OnDisk()
    {
        var repoRoot = TryFindRepoRoot();
        if (repoRoot is null)
        {
            return;
        }

        var psRoot = HelpPaths.PsModulesRoot(repoRoot);
        Assert.True(Directory.Exists(psRoot), $"Expected PS modules root at: {psRoot}");

        AssertExactSegmentCasing(repoRoot, HelpPaths.PsModulesSegments);

        // Each module's authored help must use lowercase `help` (CLI side uses capital `Help`).
        // Enumerate the module entry case-insensitively then compare the actual on-disk name
        // case-sensitively \u2014 Path.GetFileName(Path.Combine(...)) just echoes the input.
        foreach (var moduleDir in Directory.EnumerateDirectories(psRoot))
        {
            var actualHelpName = Directory
                .EnumerateDirectories(moduleDir)
                .Select(Path.GetFileName)
                .FirstOrDefault(name => string.Equals(name, HelpPaths.PsHelpFolder, StringComparison.OrdinalIgnoreCase));

            if (actualHelpName is null)
            {
                continue;
            }

            Assert.Equal(HelpPaths.PsHelpFolder, actualHelpName);
        }
    }

    [Fact]
    public void CliHelpDocs_ShouldNotHaveSiblingLowercaseHelp_OnDisk()
    {
        // Linux can hold both `src/CLI/help/` and `src/CLI/Help/`; we must not.
        var repoRoot = TryFindRepoRoot();
        if (repoRoot is null)
        {
            return;
        }

        var cliRoot = Path.Combine(repoRoot, "src", "CLI");
        if (!Directory.Exists(cliRoot))
        {
            return;
        }

        var caseVariants = Directory
            .EnumerateDirectories(cliRoot)
            .Select(Path.GetFileName)
            .Where(name => string.Equals(name, "Help", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Single(caseVariants);
        Assert.Equal("Help", caseVariants[0]);
    }

    private static void AssertExactSegmentCasing(string repoRoot, IReadOnlyList<string> segments)
    {
        var current = repoRoot;
        foreach (var expected in segments)
        {
            var actual = Directory
                .EnumerateDirectories(current)
                .Select(Path.GetFileName)
                .FirstOrDefault(name => string.Equals(name, expected, StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(actual);
            Assert.Equal(expected, actual);
            current = Path.Combine(current, actual!);
        }
    }

    private static string? TryFindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "azclips.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}
