// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Management.Automation.Language;

namespace Cirrus.Help.Tool.Validation
{
    /// <summary>
    /// Validates that every cmdlet exported by a PowerShell module's <c>.psd1</c> manifest
    /// has a corresponding <c>src/PS/&lt;Module&gt;/help/&lt;Verb-Noun&gt;.md</c> file, and
    /// warns on help files that don't correspond to any exported cmdlet. Mirrors the CLI
    /// surface's <see cref="HelpCompletenessValidator"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Per-file rules (<c>PSL1</c>..<c>PSL8</c>, plus <c>PS_TodoPlaceholder</c> /
    /// <c>PS_ExamplePlaceholder</c>) only fire on files that actually exist. Without this
    /// validator, deleting a module's <c>help/</c> folder produces a silent pass — the very
    /// gap that motivated <c>PSL9</c>/<c>PSL10</c>.
    /// </para>
    /// <para>
    /// Validation reads the source manifest at <c>src/PS/&lt;Module&gt;/&lt;Module&gt;.psd1</c>
    /// (not the staged copy under <c>artifacts/Debug/PS/</c>) so the rule fails early — before
    /// the build script's <c>Publish-CirrusHelpArtifacts</c> step silently skips modules
    /// without a <c>help/</c> folder.
    /// </para>
    /// </remarks>
    public sealed class PsCompletenessValidator
    {
        /// <summary>
        /// Walks every <c>src/PS/&lt;Module&gt;/</c> directory and validates help-file coverage
        /// against the cmdlets declared in the module's <c>&lt;Module&gt;.psd1</c>.
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
                    // Not a PowerShell module folder (e.g. shared helpers). Skip silently;
                    // this validator only targets folders that ship as a module.
                    continue;
                }

                var exportedCmdlets = ParseCmdletsToExport(manifestPath);
                if (exportedCmdlets.Count == 0)
                {
                    // Module exports nothing (or uses `*` wildcard, which we treat as
                    // "deferred to runtime"). Either way there's nothing to validate.
                    continue;
                }

                var helpDir = Path.Combine(moduleDir, Core.Cli.Help.HelpPaths.PsHelpFolder);
                var manifestRelativePath = Path.GetRelativePath(repoRoot, manifestPath);

                if (!Directory.Exists(helpDir))
                {
                    // Single error per module so multi-cmdlet modules don't drown the log.
                    // Per-cmdlet PSL9 errors are suppressed when PSL10 fires.
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
        /// Parses <paramref name="manifestPath"/> using the PowerShell AST and extracts
        /// string literals from the <c>CmdletsToExport</c> hashtable entry. Wildcard exports
        /// (<c>'*'</c>) are filtered out — they bypass static validation by design.
        /// </summary>
        internal static IReadOnlyList<string> ParseCmdletsToExport(string manifestPath)
        {
            ScriptBlockAst ast;
            try
            {
                ast = Parser.ParseFile(manifestPath, out _, out var errors);
                if (errors.Length > 0)
                {
                    // Manifest is syntactically broken. Don't double-report — let other
                    // tooling (PlatyPS, Test-ModuleManifest) surface the parse error.
                    return Array.Empty<string>();
                }
            }
            catch
            {
                return Array.Empty<string>();
            }

            // The .psd1 root is a script block whose body is a single hashtable expression.
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
                    // Wildcard export ⇒ caller treats whole module as opt-out.
                    continue;
                }
                result.Add(literal);
            }
            return result;
        }
    }
}
