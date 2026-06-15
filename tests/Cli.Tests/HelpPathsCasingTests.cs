// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Cli.Help;

namespace Cli.Tests;

/// <summary>
/// Pin the on-disk casing of help-system folders. Windows is case-insensitive,
/// so a drift like <c>src/CLI/help/</c> (lowercase) compiles and runs locally
/// while Linux CI silently sees zero help files. These tests fail loudly the
/// moment that drift appears so the bug doesn't ship.
/// </summary>
/// <remarks>
/// Runs against the live repo by walking up to <c>cirrus.slnx</c>, mirroring how
/// <see cref="MarkdownCommandHelpProvider"/> resolves the source tree at dev time.
/// Skipped if the test process can't find the repo root (e.g. a packaged build).
/// </remarks>
public class HelpPathsCasingTests
{
    [Fact]
    public void CliHelpDocs_ShouldExistWithExactCasing_OnDisk()
    {
        var repoRoot = TryFindRepoRoot();
        if (repoRoot is null)
        {
            return; // not running from a dev tree; nothing to assert
        }

        var helpDocs = HelpPaths.CliHelpDocsRoot(repoRoot);
        Assert.True(Directory.Exists(helpDocs), $"Expected help docs root at: {helpDocs}");

        // Walk down each segment and assert the directory entry matches our constant
        // case-sensitively. Path.Combine + Directory.Exists alone is case-insensitive
        // on Windows; we have to enumerate the parent and compare names.
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

        // Each module that ships authored help must use the lowercase `help` folder.
        // The CLI side is `Help` (capital H) by design — different surface, different convention.
        foreach (var moduleDir in Directory.EnumerateDirectories(psRoot))
        {
            var helpDir = Path.Combine(moduleDir, HelpPaths.PsHelpFolder);
            if (!Directory.Exists(helpDir))
            {
                continue; // module has no authored help; not all modules do
            }

            var actualName = Path.GetFileName(helpDir);
            Assert.Equal(HelpPaths.PsHelpFolder, actualName);
        }
    }

    [Fact]
    public void CliHelpDocs_ShouldNotHaveSiblingLowercaseHelp_OnDisk()
    {
        // Guard against a `src/CLI/help/` (lowercase) sneaking in beside `src/CLI/Help/`.
        // Filesystems on Linux happily hold both; we must not.
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
            if (File.Exists(Path.Combine(current.FullName, "cirrus.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}
