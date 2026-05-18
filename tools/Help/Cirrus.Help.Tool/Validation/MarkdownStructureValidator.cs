// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Core.Runtime;

namespace Cirrus.Help.Tool.Validation
{
    /// <summary>
    /// Validates that markdown help files under <c>src/Extensions/{Extension}/help/</c> meet
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
            var helpRoot = Path.Combine(repoRoot, "src", "Extensions");

            if (!Directory.Exists(helpRoot))
            {
                report.AddError(ProblemIds.MD_ExtensionsDirMissing, $"Extensions directory not found: {helpRoot}");
                return report;
            }

            var markdownFiles = Directory
                .EnumerateFiles(helpRoot, "*.md", SearchOption.AllDirectories)
                .Where(static path => path.Contains($"{Path.DirectorySeparatorChar}help{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (markdownFiles.Count == 0)
            {
                report.AddError(ProblemIds.MD_NoMarkdownFiles, "No help markdown files found under src/Extensions/*/help.");
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
            if (!HasBalancedCodeFences(content))
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
            var description = GetMarkdownField(body, "Description");
            if (string.IsNullOrWhiteSpace(description))
            {
                report.AddError(ProblemIds.MD_MissingDescription, "Missing required field 'Description:'.", relativePath);
            }

            var exampleCount = Regex.Matches(
                body,
                @"###\s*(?<title>.+?)\s*\r?\n```(?<lang>[a-zA-Z0-9_-]*)\s*\r?\n(?<command>[\s\S]*?)\r?\n```",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase).Count;

            if (exampleCount == 0)
            {
                report.AddError(
                    ProblemIds.MD_NoExamples,
                    "Missing examples. Add at least one '### Title' + fenced code block example.",
                    relativePath);
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

        private static string GetMarkdownField(string content, string fieldName)
        {
            var match = Regex.Match(
                content,
                $@"^\s*{Regex.Escape(fieldName)}\s*:\s*(?<value>[\s\S]*?)(?=^\s*[A-Za-z][A-Za-z0-9_-]*\s*:|\z)",
                RegexOptions.Multiline | RegexOptions.CultureInvariant);

            return match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
        }

        private static bool HasBalancedCodeFences(string content)
        {
            var fenceCount = Regex.Matches(content, @"(?m)^\s*```[a-zA-Z0-9_-]*\s*$", RegexOptions.CultureInvariant).Count;
            return fenceCount % 2 == 0;
        }
    }
}
