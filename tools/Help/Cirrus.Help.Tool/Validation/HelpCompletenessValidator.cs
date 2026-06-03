// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Cirrus.Help.Tool.Validation
{
    /// <summary>
    /// Validates that every routed CLI command in an extension has a corresponding
    /// help markdown file, and warns on help files that aren't referenced by any
    /// routed command.
    /// </summary>
    public sealed class HelpCompletenessValidator
    {
        /// <summary>
        /// Validates help coverage for a single extension against its repository folder.
        /// </summary>
        /// <param name="repoRoot">Repository root (folder containing <c>src/</c>).</param>
        /// <param name="extensionName">Lowercase extension name (e.g. <c>login</c>).</param>
        /// <param name="extensionIndex">Index providing the routed CLI command table.</param>
        public ValidationReport Validate(string repoRoot, string extensionName, IExtensionIndex extensionIndex)
        {
            var extensionFolder = HelpCommandKey.ToExtensionFolder(extensionName);
            var helpDirectory = Path.Combine(repoRoot, "src", "CLI", "Help", extensionFolder);

            return Validate(extensionName, extensionFolder, helpDirectory, extensionIndex);
        }

        /// <summary>
        /// Lower-level overload used by tests: callers pass the help directory directly
        /// so the validator can be exercised against a temp folder.
        /// </summary>
        public ValidationReport Validate(
            string extensionName,
            string extensionFolder,
            string helpDirectory,
            IExtensionIndex extensionIndex)
        {
            var report = new ValidationReport();

            var routedCommandKeys = extensionIndex
                .GetCommandTable(ClientType.CLI)
                .Keys
                .Select(static key => HelpCommandKey.Normalize(key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var expectedFiles = routedCommandKeys
                .Select(static key => HelpCommandKey.ToHelpFileName(key))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            expectedFiles.Add("index.md");

            var actualFiles = Directory.Exists(helpDirectory)
                ? Directory
                    .EnumerateFiles(helpDirectory, "*.md", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(helpDirectory, path)
                        .Replace(Path.DirectorySeparatorChar, '/')
                        .Replace(Path.AltDirectorySeparatorChar, '/'))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var missing = expectedFiles
                .Except(actualFiles, StringComparer.OrdinalIgnoreCase)
                .OrderBy(static file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var file in missing)
            {
                var relativePath = $"src{Path.DirectorySeparatorChar}CLI{Path.DirectorySeparatorChar}help{Path.DirectorySeparatorChar}{extensionFolder}{Path.DirectorySeparatorChar}{file}";
                report.AddError(
                    ProblemIds.Completeness_MissingHelpFile,
                    $"Missing help file for routed command '{HelpCommandKey.FromHelpFileName(file)}' in extension '{extensionName}'.",
                    relativePath);
            }

            var orphaned = actualFiles
                .Except(expectedFiles, StringComparer.OrdinalIgnoreCase)
                .OrderBy(static file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var orphan in orphaned)
            {
                var orphanPath = $"src{Path.DirectorySeparatorChar}CLI{Path.DirectorySeparatorChar}help{Path.DirectorySeparatorChar}{extensionFolder}{Path.DirectorySeparatorChar}{orphan}";
                report.AddWarning(
                    ProblemIds.Completeness_OrphanedHelpFile,
                    $"Help file is not referenced by any routed CLI command in extension '{extensionName}'.",
                    orphanPath);
            }

            return report;
        }
    }
}
