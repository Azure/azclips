// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Cli.Help;

namespace Cirrus.Help.Tool.Validation
{
    /// <summary>
    /// Validates that markdown help files under <c>src/CLI/Help/Docs/{extension}/</c> meet
    /// the required structural contract: file-level frontmatter with required
    /// fields, a Description body, balanced code fences, and at least one example.
    /// </summary>
    public sealed class MarkdownStructureValidator
    {
        /// <summary>
        /// Validates every help markdown file under the given repo root and returns
        /// a report of all issues. Does not throw on validation failures; callers decide.
        /// </summary>
        /// <param name="repoRoot">Repository root (folder containing <c>src/</c>).</param>
        public ValidationReport Validate(string repoRoot)
        {
            var report = new ValidationReport();
            var helpRoot = HelpPaths.CliHelpDocsRoot(repoRoot);

            if (!Directory.Exists(helpRoot))
            {
                report.AddError(ProblemIds.MD_ExtensionsDirMissing, $"CLI help directory not found: {helpRoot}");
                return report;
            }

            var markdownFiles = Directory
                .EnumerateFiles(helpRoot, "*.md", SearchOption.AllDirectories)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (markdownFiles.Count == 0)
            {
                report.AddError(ProblemIds.MD_NoMarkdownFiles, "No help markdown files found under src/CLI/Help/Docs/*.");
                return report;
            }

            foreach (var file in markdownFiles)
            {
                var content = File.ReadAllText(file);
                var relativePath = Path.GetRelativePath(repoRoot, file);
                ValidateContent(content, relativePath, report);
            }

            return report;
        }

        /// <summary>
        /// I/O-free variant exposed for unit testing. Validates a single markdown document.
        /// </summary>
        public ValidationReport ValidateContent(string content, string filePath)
        {
            var report = new ValidationReport();
            ValidateContent(content, filePath, report);
            return report;
        }

        private static void ValidateContent(string content, string relativePath, ValidationReport report)
        {
            if (!MarkdownHelpParser.HasBalancedCodeFences(content))
            {
                report.AddError(ProblemIds.MD_UnbalancedFences, "Unbalanced markdown code fences (``` markers must be paired).", relativePath);
            }

            // Every help file must carry a YAML frontmatter block at the top.
            if (!HasFrontmatterBlock(content))
            {
                report.AddError(
                    ProblemIds.MD_MissingFrontmatter,
                    "Missing required YAML frontmatter block. Help files must open with a '---' delimited block.",
                    relativePath);
                // Without frontmatter, downstream summary/status/schema checks are meaningless.
                ValidateBody(content, relativePath, report);
                return;
            }

            var (frontmatter, body) = FrontmatterParser.Parse(content);

            if (string.IsNullOrWhiteSpace(frontmatter.Summary))
            {
                report.AddError(ProblemIds.MD_MissingSummary, "Frontmatter is missing required field 'summary:'.", relativePath);
            }
            else if (PlaceholderDetector.ContainsTodo(frontmatter.Summary))
            {
                report.AddError(
                    ProblemIds.MD_TodoPlaceholder,
                    "Frontmatter 'summary:' still contains a TODO marker. Replace the scaffolded placeholder with a real description.",
                    relativePath);
            }

            if (string.IsNullOrWhiteSpace(frontmatter.Schema))
            {
                report.AddError(ProblemIds.MD_MissingSchema, "Frontmatter is missing required field 'schema:'.", relativePath);
            }

            // status defaults to "ga" inside the parser, so a missing key is fine. Reject
            // unknown values so typos like "stable" surface early.
            if (!FrontmatterParser.ValidStatuses.Contains(frontmatter.Status))
            {
                report.AddError(
                    ProblemIds.MD_InvalidStatus,
                    $"Frontmatter 'status' value '{frontmatter.Status}' is not one of {{{string.Join(", ", FrontmatterParser.ValidStatuses)}}}.",
                    relativePath);
            }

            ValidateBody(body, relativePath, report);
        }

        private static void ValidateBody(string body, string relativePath, ValidationReport report)
        {
            var description = MarkdownHelpParser.GetField(body, "Description");
            if (string.IsNullOrWhiteSpace(description))
            {
                report.AddError(ProblemIds.MD_MissingDescription, "Missing required field 'Description:'.", relativePath);
            }
            else if (PlaceholderDetector.ContainsTodo(description))
            {
                report.AddError(
                    ProblemIds.MD_TodoPlaceholder,
                    "Body field 'Description:' still contains a TODO marker. Replace the scaffolded placeholder with real content.",
                    relativePath);
            }

            // Shared parser keeps the validator's "no examples" decision aligned with the runtime renderer.
            var examples = MarkdownHelpParser.EnumerateExamples(body).ToList();

            if (examples.Count == 0)
            {
                report.AddError(
                    ProblemIds.MD_NoExamples,
                    "Missing examples. Add at least one '### Title' + fenced code block example.",
                    relativePath);
            }

            // Scan example bodies + titles for TODO and angle-bracket placeholders.
            // Angle-bracket rule fires only inside CLI fences — prose can legitimately mention `<name>`.
            foreach (var example in examples)
            {
                if (PlaceholderDetector.ContainsTodo(example.Title))
                {
                    report.AddError(
                        ProblemIds.MD_TodoPlaceholder,
                        $"Example heading '### {example.Title}' still contains a TODO marker.",
                        relativePath);
                }

                if (PlaceholderDetector.ContainsTodo(example.Command))
                {
                    report.AddError(
                        ProblemIds.MD_TodoPlaceholder,
                        $"Example fence under '### {example.Title}' still contains a TODO marker. Replace the scaffolded placeholder with a real invocation.",
                        relativePath);
                }

                // PS fences are validated by PowerShellExampleSyntaxValidator; skip here.
                var isCliFence = example.Language.Equals("bash", StringComparison.OrdinalIgnoreCase)
                    || example.Language.Equals("sh", StringComparison.OrdinalIgnoreCase);
                if (!isCliFence)
                {
                    continue;
                }

                foreach (var placeholder in PlaceholderDetector.EnumerateAngleBracketPlaceholders(example.Command))
                {
                    report.AddError(
                        ProblemIds.CLI_ExamplePlaceholder,
                        $"Example contains placeholder '{placeholder}'. Replace with a realistic literal value (e.g. `myrg`, `eastus`, `00000000-0000-0000-0000-000000000000`) or a `$variable`.",
                        relativePath);
                }
            }
        }

        private static bool HasFrontmatterBlock(string content)
        {
            // Quick check: first non-empty line is exactly '---'.
            foreach (var line in content.Split('\n'))
            {
                var trimmed = line.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                return trimmed == "---";
            }

            return false;
        }
    }
}
