// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli.Help;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Azclips.Help.Tool.Validation;

/// <summary>
/// Runs Markdig over each CLI help markdown file and flags patterns that render as surprising
/// HTML through docfx's downstream Markdig pipeline. Complements
/// <see cref="MarkdownStructureValidator"/> — that one catches file-structure issues (frontmatter,
/// fence balance, placeholder markers); this one catches inline-prose issues (HTML-looking
/// placeholders, unbalanced backticks) that pass structural checks but break at render time.
/// </summary>
/// <remarks>
/// Fenced code blocks are excluded from all inspections — their content is verbatim and
/// separately validated by <see cref="ExampleSyntaxValidator"/> /
/// <see cref="PowerShellExampleSyntaxValidator"/>.
/// </remarks>
public sealed class MarkdigWellFormednessValidator
{
    private static readonly MarkdownPipeline s_pipeline = new MarkdownPipelineBuilder()
        .UsePreciseSourceLocation()
        .Build();

    /// <summary>
    /// A small allowlist of inline HTML tag names we accept as intentional. Anything outside
    /// this list that Markdig identifies as an <see cref="HtmlInline"/> is flagged as a
    /// suspicious placeholder (e.g. <c>&lt;name&gt;</c>, <c>&lt;vm-size&gt;</c>). Extend only
    /// when a real authoring case demands it.
    /// </summary>
    private static readonly HashSet<string> s_allowedHtmlTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "br", "code", "em", "i", "img", "kbd", "p", "strong", "sub", "sup", "b",
    };

    public ValidationReport Validate(string repoRoot)
    {
        var report = new ValidationReport();
        var helpRoot = HelpPaths.CliHelpDocsRoot(repoRoot);

        if (!Directory.Exists(helpRoot))
        {
            return report;
        }

        var markdownFiles = Directory
            .EnumerateFiles(helpRoot, "*.md", SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase);

        foreach (var file in markdownFiles)
        {
            var content = File.ReadAllText(file);
            var relativePath = Path.GetRelativePath(repoRoot, file);
            ValidateContent(content, relativePath, report);
        }

        return report;
    }

    internal static void ValidateContent(string content, string relativePath, ValidationReport report)
    {
        var document = Markdown.Parse(content, s_pipeline);
        CheckSuspiciousHtmlInlines(document, relativePath, report);
        CheckUnbalancedBackticks(document, relativePath, report);
    }

    private static void CheckSuspiciousHtmlInlines(MarkdownDocument document, string relativePath, ValidationReport report)
    {
        foreach (var htmlInline in document.Descendants<HtmlInline>())
        {
            var tagText = htmlInline.Tag;
            if (string.IsNullOrEmpty(tagText))
            {
                continue;
            }

            var name = ExtractTagName(tagText);
            if (name is null || s_allowedHtmlTags.Contains(name))
            {
                continue;
            }

            var line = htmlInline.Line + 1;
            report.AddError(
                ProblemIds.MD_SuspiciousHtmlTag,
                $"line {line}: '{tagText}' looks like a placeholder rendered as HTML. Wrap in backticks or rewrite (e.g. `{name}` instead of <{name}>).",
                relativePath,
                line);
        }
    }

    private static void CheckUnbalancedBackticks(MarkdownDocument document, string relativePath, ValidationReport report)
    {
        foreach (var paragraph in document.Descendants<ParagraphBlock>())
        {
            if (paragraph.Inline is null)
            {
                continue;
            }

            foreach (var literal in paragraph.Inline.Descendants<LiteralInline>())
            {
                var text = literal.Content.ToString();
                var backtickCount = text.Count(c => c == '`');
                if (backtickCount % 2 != 0)
                {
                    var line = literal.Line + 1;
                    var excerpt = BuildExcerpt(text);
                    report.AddError(
                        ProblemIds.MD_UnbalancedBackticks,
                        $"line {line}: odd number of backticks ({backtickCount}) in prose — likely an unclosed inline-code span. near: \"{excerpt}\"",
                        relativePath,
                        line);
                }
            }
        }
    }

    // ±20 chars around the last backtick, collapsed to single-line, with ellipses when trimmed.
    private static string BuildExcerpt(string text)
    {
        var lastBacktick = text.LastIndexOf('`');
        if (lastBacktick < 0)
        {
            return text;
        }

        const int Radius = 20;
        var start = Math.Max(0, lastBacktick - Radius);
        var end = Math.Min(text.Length, lastBacktick + Radius + 1);
        var slice = text[start..end].Replace('\r', ' ').Replace('\n', ' ').Replace("\"", "\\\"");
        var prefix = start > 0 ? "…" : string.Empty;
        var suffix = end < text.Length ? "…" : string.Empty;
        return $"{prefix}{slice}{suffix}";
    }

    // Extracts the tag NAME from raw HTML-like content Markdig recognised as inline HTML.
    // Examples: "<name>" -> "name"; "<vm-size>" -> "vm-size"; "</a>" -> "a";
    // "<img src=... />" -> "img". Returns null when parsing fails.
    private static string? ExtractTagName(string raw)
    {
        var start = raw.StartsWith("</", StringComparison.Ordinal) ? 2 : 1;
        if (start >= raw.Length)
        {
            return null;
        }

        var end = start;
        while (end < raw.Length)
        {
            var ch = raw[end];
            if (!char.IsLetterOrDigit(ch) && ch != '-' && ch != '_')
            {
                break;
            }
            end++;
        }

        return end > start ? raw[start..end] : null;
    }
}
