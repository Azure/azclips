// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Cirrus.Help.Tool.Validation
{
    /// <summary>
    /// Validates that every routed CLI command in a verb's namespace has a corresponding
    /// help markdown file under <c>src/CLI/Help/Docs/&lt;verb&gt;/</c>, and warns on help
    /// files that aren't referenced by any routed command.
    /// </summary>
    /// <remarks>
    /// Validation is verb-scoped (matching <see cref="Core.Cli.Help.MarkdownCommandHelpProvider"/>'s
    /// runtime lookup). A multi-verb extension like Compute is validated once per verb
    /// (<c>vm</c>, <c>disk</c>) — each pass sees only the commands belonging to that verb.
    /// </remarks>
    public sealed class HelpCompletenessValidator
    {
        /// <summary>
        /// Validates help coverage for a single verb against its repository folder.
        /// </summary>
        /// <param name="repoRoot">Repository root (folder containing <c>src/</c>).</param>
        /// <param name="verb">Lowercase CLI verb (e.g. <c>vm</c>, <c>disk</c>, <c>login</c>).</param>
        /// <param name="extensionIndex">Index providing the routed CLI command table (full table; the validator scopes to the verb).</param>
        public ValidationReport Validate(string repoRoot, string verb, IExtensionIndex extensionIndex)
        {
            var verbFolder = HelpCommandKey.ToExtensionFolder(verb);
            var helpDirectory = Core.Cli.Help.HelpPaths.CliVerbHelpDir(repoRoot, verbFolder);

            return Validate(verb, verbFolder, helpDirectory, extensionIndex);
        }

        /// <summary>
        /// Lower-level overload used by tests: callers pass the help directory directly
        /// so the validator can be exercised against a temp folder.
        /// </summary>
        public ValidationReport Validate(
            string verb,
            string verbFolder,
            string helpDirectory,
            IExtensionIndex extensionIndex)
        {
            var report = new ValidationReport();

            // Scope the routed command table to the verb. Keys arrive as "verb",
            // "verb path"; emit the relative form ("", "path") that matches the
            // help file lookup convention (index.md, path.md).
            var routedCommandKeys = ScopeKeysToVerb(
                extensionIndex.GetCommandTable(ClientType.CLI).Keys,
                verb);

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
                var relativePath = $"src{Path.DirectorySeparatorChar}CLI{Path.DirectorySeparatorChar}Help{Path.DirectorySeparatorChar}Docs{Path.DirectorySeparatorChar}{verbFolder}{Path.DirectorySeparatorChar}{file}";
                // index.md backs the verb root itself, so render as the verb name rather than
                // an empty-quoted command path (which reads as "routed command ''").
                var commandPath = file.Equals("index.md", StringComparison.OrdinalIgnoreCase)
                    ? verb
                    : HelpCommandKey.FromHelpFileName(file);
                report.AddError(
                    ProblemIds.Completeness_MissingHelpFile,
                    $"Missing help file for routed command '{commandPath}' in verb '{verb}'.",
                    relativePath);
            }

            var orphaned = actualFiles
                .Except(expectedFiles, StringComparer.OrdinalIgnoreCase)
                .OrderBy(static file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var orphan in orphaned)
            {
                var orphanPath = $"src{Path.DirectorySeparatorChar}CLI{Path.DirectorySeparatorChar}Help{Path.DirectorySeparatorChar}Docs{Path.DirectorySeparatorChar}{verbFolder}{Path.DirectorySeparatorChar}{orphan}";
                report.AddWarning(
                    ProblemIds.Completeness_OrphanedHelpFile,
                    $"Help file is not referenced by any routed CLI command in verb '{verb}'.",
                    orphanPath);
            }

            return report;
        }

        /// <summary>
        /// Filters <paramref name="rawKeys"/> down to those owned by <paramref name="verb"/>
        /// and strips the verb prefix. Mirrors <c>ExtensionIndexHelpAdapter.BuildVerbScopedTable</c>
        /// so validation matches what the runtime help renderer actually sees.
        /// </summary>
        private static IEnumerable<string> ScopeKeysToVerb(IEnumerable<string> rawKeys, string verb)
        {
            var prefix = verb + " ";
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in rawKeys)
            {
                var key = HelpCommandKey.Normalize(raw);
                string? scoped = null;

                if (key.Equals(verb, StringComparison.OrdinalIgnoreCase))
                {
                    scoped = string.Empty;
                }
                else if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    scoped = key[prefix.Length..];
                }

                if (scoped is not null && seen.Add(scoped))
                {
                    yield return scoped;
                }
            }
        }
    }
}
