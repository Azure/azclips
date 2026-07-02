// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Management.Automation.Language;

namespace Azclips.Help.Tool.Validation
{
    /// <summary>
    /// Validates that every cmdlet exported by a PowerShell module's <c>.psd1</c> manifest
    /// has a corresponding <c>src/PS/&lt;Module&gt;/help/&lt;Verb-Noun&gt;.md</c> file, and warns
    /// on orphaned help files. Mirrors the CLI surface's <see cref="HelpCompletenessValidator"/>.
    /// </summary>
    /// <remarks>
    /// Reads the source manifest at <c>src/PS/&lt;Module&gt;/&lt;Module&gt;.psd1</c> (not the
    /// staged copy under <c>artifacts/Debug/PS/</c>) so the rule fails before the build
    /// script's <c>Publish-AzclipsHelpArtifacts</c> step silently skips modules without help.
    /// </remarks>
    public sealed class PsCompletenessValidator
    {
        /// <summary>
        /// Walks every <c>src/PS/&lt;Module&gt;/</c> directory and validates help-file coverage.
        /// </summary>
        public ValidationReport Validate(string repoRoot)
        {
            var report = new ValidationReport();
            var psRoot = Core.Cli.Help.HelpPaths.PsModulesRoot(repoRoot);

            if (!Directory.Exists(psRoot))
            {
                return report;
            }

            foreach (var moduleDir in Directory
                         .EnumerateDirectories(psRoot)
                         .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                var moduleName = Path.GetFileName(moduleDir);
                var manifestPath = Path.Combine(moduleDir, $"{moduleName}.psd1");
                if (!File.Exists(manifestPath))
                {
                    // Not a PS module folder (e.g. shared helpers). Skip silently.
                    continue;
                }

                var exportedCmdlets = ParseCmdletsToExport(manifestPath);
                if (exportedCmdlets.Count == 0)
                {
                    // No exports, or `*` wildcard (deferred to runtime). Nothing to validate.
                    continue;
                }

                var helpDir = Path.Combine(moduleDir, Core.Cli.Help.HelpPaths.PsHelpFolder);
                var manifestRelativePath = Path.GetRelativePath(repoRoot, manifestPath);

                if (!Directory.Exists(helpDir))
                {
                    // PSL10 supersedes per-cmdlet PSL9 errors so multi-cmdlet modules don't drown the log.
                    report.AddError(
                        ProblemIds.PSL10_MissingHelpFolder,
                        $"PowerShell module '{moduleName}' exports {exportedCmdlets.Count} cmdlet(s) but has no '{Core.Cli.Help.HelpPaths.PsHelpFolder}/' folder. Run 'build/New-PSHelpMarkdown.ps1 -Module {moduleName}' to scaffold help stubs, then fill in the content.",
                        manifestRelativePath);
                    continue;
                }

                ValidateModule(report, repoRoot, moduleName, helpDir, exportedCmdlets);
            }

            return report;
        }

        private static void ValidateModule(
            ValidationReport report,
            string repoRoot,
            string moduleName,
            string helpDir,
            IReadOnlyList<string> exportedCmdlets)
        {
            var expectedFiles = exportedCmdlets
                .Select(static cmdlet => $"{cmdlet}.md")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var actualFiles = Directory
                .EnumerateFiles(helpDir, "*.md", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(static name => name is not null)
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var missing in expectedFiles
                         .Except(actualFiles, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase))
            {
                var expectedPath = Path.GetRelativePath(repoRoot, Path.Combine(helpDir, missing));
                var cmdletName = Path.GetFileNameWithoutExtension(missing);
                report.AddError(
                    ProblemIds.PSL9_MissingHelpFile,
                    $"Missing help file for cmdlet '{cmdletName}' exported by module '{moduleName}'. Run 'build/New-PSHelpMarkdown.ps1 -Module {moduleName}' to scaffold the stub, then fill in the content.",
                    expectedPath);
            }

            foreach (var orphan in actualFiles
                         .Except(expectedFiles, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase))
            {
                var orphanPath = Path.GetRelativePath(repoRoot, Path.Combine(helpDir, orphan));
                report.AddWarning(
                    ProblemIds.PSL11_OrphanedHelpFile,
                    $"Help file '{orphan}' does not correspond to any cmdlet exported by module '{moduleName}'. Either add the cmdlet to '{moduleName}.psd1' CmdletsToExport, or delete the help file.",
                    orphanPath);
            }
        }

        /// <summary>
        /// Parses <paramref name="manifestPath"/> with the PS AST and extracts string
        /// literals from <c>CmdletsToExport</c>. Wildcards (<c>'*'</c>) are filtered out.
        /// </summary>
        internal static IReadOnlyList<string> ParseCmdletsToExport(string manifestPath)
        {
            ScriptBlockAst ast;
            try
            {
                ast = Parser.ParseFile(manifestPath, out _, out var errors);
                if (errors.Length > 0)
                {
                    // Don't double-report; PlatyPS / Test-ModuleManifest will surface the parse error.
                    return Array.Empty<string>();
                }
            }
            catch
            {
                return Array.Empty<string>();
            }

            // .psd1 root is a script block whose body is a single hashtable.
            var hashtable = ast.Find(static node => node is HashtableAst, searchNestedScriptBlocks: true) as HashtableAst;
            if (hashtable is null)
            {
                return Array.Empty<string>();
            }

            foreach (var (keyAst, valueAst) in hashtable.KeyValuePairs)
            {
                if (keyAst is not StringConstantExpressionAst keyName)
                {
                    continue;
                }

                if (!string.Equals(keyName.Value, "CmdletsToExport", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return ExtractStringLiterals(valueAst);
            }

            return Array.Empty<string>();
        }

        private static IReadOnlyList<string> ExtractStringLiterals(StatementAst valueAst)
        {
            var result = new List<string>();
            foreach (var node in valueAst.FindAll(static n => n is StringConstantExpressionAst, searchNestedScriptBlocks: true))
            {
                var literal = ((StringConstantExpressionAst)node).Value;
                if (string.IsNullOrWhiteSpace(literal) || literal == "*")
                {
                    // Wildcard export => caller treats whole module as opt-out.
                    continue;
                }
                result.Add(literal);
            }
            return result;
        }
    }
}
