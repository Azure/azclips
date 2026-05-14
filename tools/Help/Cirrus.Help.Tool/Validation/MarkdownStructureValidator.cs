// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Cirrus.Help.Tool.Validation
{
    /// <summary>
    /// Validates that markdown help files under <c>src/Extensions/{Extension}/help/</c> meet
    /// the required structural contract: balanced code fences, Summary/Description fields,
    /// and at least one example.
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
                report.AddError($"Extensions directory not found: {helpRoot}");
                return report;
            }

            var markdownFiles = Directory
                .EnumerateFiles(helpRoot, "*.md", SearchOption.AllDirectories)
                .Where(static path => path.Contains($"{Path.DirectorySeparatorChar}help{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (markdownFiles.Count == 0)
            {
                report.AddError("No help markdown files found under src/Extensions/*/help.");
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
                report.AddError("Unbalanced markdown code fences (``` markers must be paired).", relativePath);
            }

            var summary = GetMarkdownField(content, "Summary");
            if (string.IsNullOrWhiteSpace(summary))
            {
                report.AddError("Missing required field 'Summary:'.", relativePath);
            }

            var description = GetMarkdownField(content, "Description");
            if (string.IsNullOrWhiteSpace(description))
            {
                report.AddError("Missing required field 'Description:'.", relativePath);
            }

            var exampleCount = Regex.Matches(
                content,
                @"###\s*(?<title>.+?)\s*\r?\n```(?<lang>[a-zA-Z0-9_-]*)\s*\r?\n(?<command>[\s\S]*?)\r?\n```",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase).Count;

            if (exampleCount == 0)
            {
                report.AddError(
                    "Missing examples. Add at least one '### Title' + fenced code block example.",
                    relativePath);
            }
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
