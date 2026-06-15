// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.RegularExpressions;

namespace Core.Cli.Help;

/// <summary>
/// One example fence found in a help markdown body. Used by both the runtime help
/// renderer and the build-time help validators so they agree on what counts as an
/// example.
/// </summary>
public readonly record struct MarkdownHelpExample(string Title, string Language, string Command);

/// <summary>
/// Shared markdown parsing primitives for help files. The runtime renderer
/// (<see cref="MarkdownCommandHelpProvider"/>) and the build-time validators
/// (<c>Cirrus.Help.Tool</c>) both call into this class so their notion of
/// "what is a Description field" and "what is an example fence" cannot drift.
/// </summary>
/// <remarks>
/// Lenient by design — malformed input yields empty/partial results rather than
/// throwing. Strict checks live in <c>MarkdownStructureValidator</c>.
/// </remarks>
public static class MarkdownHelpParser
{
    // Title line, optional blank line(s), then the opening ``` fence. Markdown
    // authors commonly leave a blank line between the H3 heading and the fence;
    // a tighter `\s*\r?\n```` form silently drops every example in those files.
    private static readonly Regex s_exampleRegex = new(
        @"###\s*(?<title>[^\r\n]+?)[ \t]*\r?\n(?:[ \t]*\r?\n)*```(?<lang>[a-zA-Z0-9_-]*)[ \t]*\r?\n(?<command>[\s\S]*?)\r?\n```",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Returns the regex used to locate <c>### Title</c> + fenced-code-block example pairs
    /// in markdown bodies. Exposed for validators that need a count or per-match positions
    /// rather than the materialized <see cref="MarkdownHelpExample"/> sequence.
    /// </summary>
    public static Regex ExampleRegex => s_exampleRegex;

    /// <summary>
    /// Reads a <c>Name: value</c> body field, including any continuation lines that follow.
    /// A continuation line is any non-blank, non-heading, non-fence line that comes after
    /// the field's first line — matches the PR-#226 authoring convention demonstrated in
    /// the structure-validator tests (<c>Description:</c> followed by indented continuation).
    /// </summary>
    /// <param name="content">Markdown body (frontmatter already stripped, or full file content).</param>
    /// <param name="fieldName">Field name without the trailing colon (e.g. <c>"Description"</c>).</param>
    /// <returns>The collapsed value, or empty string when the field is absent.</returns>
    public static string GetField(string content, string fieldName)
    {
        if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(fieldName))
        {
            return string.Empty;
        }

        var lines = content.Split('\n');
        var prefix = fieldName + ":";
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r').TrimStart();
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var builder = new StringBuilder(line[prefix.Length..].Trim());
            for (var j = i + 1; j < lines.Length; j++)
            {
                var next = lines[j].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(next)
                    || next.TrimStart().StartsWith('#')
                    || next.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    break;
                }

                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(next.Trim());
            }

            return builder.ToString().Trim();
        }

        return string.Empty;
    }

    /// <summary>
    /// Enumerates <c>### Title</c> + fenced-code-block pairs in the supplied markdown body
    /// in document order. Empty (whitespace-only) command bodies are skipped. The language
    /// tag is returned as authored (lower-cased by the regex match group is not enforced —
    /// callers that need case-insensitive comparison should use
    /// <see cref="StringComparison.OrdinalIgnoreCase"/>).
    /// </summary>
    public static IEnumerable<MarkdownHelpExample> EnumerateExamples(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            yield break;
        }

        foreach (Match match in s_exampleRegex.Matches(content))
        {
            var title = match.Groups["title"].Value.Trim();
            var language = match.Groups["lang"].Value.Trim();
            var command = match.Groups["command"].Value.Trim();
            if (command.Length == 0)
            {
                continue;
            }

            yield return new MarkdownHelpExample(
                string.IsNullOrEmpty(title) ? "Example" : title,
                language,
                command);
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when every <c>```</c> fence in the document is paired.
    /// Counts opening fences on their own line; a stray odd count signals an unterminated
    /// example block.
    /// </summary>
    public static bool HasBalancedCodeFences(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return true;
        }

        var fenceCount = Regex.Matches(content, @"(?m)^\s*```[a-zA-Z0-9_-]*\s*$", RegexOptions.CultureInvariant).Count;
        return fenceCount % 2 == 0;
    }
}
