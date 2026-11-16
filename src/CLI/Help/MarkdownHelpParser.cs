// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.RegularExpressions;

namespace Core.Cli.Help;

/// <summary>One example fence in a help markdown body.</summary>
public readonly record struct MarkdownHelpExample(string Title, string Language, string Command);

/// <summary>
/// Shared markdown parsing primitives for help files. Used by the runtime renderer
/// (<see cref="MarkdownCommandHelpProvider"/>) and the build-time validators so they
/// can't drift on what counts as a Description field or an example fence.
/// </summary>
/// <remarks>Lenient — malformed input yields partial results. Strict checks live in <c>MarkdownStructureValidator</c>.</remarks>
public static class MarkdownHelpParser
{
    // Title, optional blank line(s), then the opening fence. Authors commonly leave a blank line
    // between the H3 heading and the fence; a tighter `\s*\r?\n```` form would silently drop them.
    private static readonly Regex s_exampleRegex = new(
        @"###\s*(?<title>[^\r\n]+?)[ \t]*\r?\n(?:[ \t]*\r?\n)*```(?<lang>[a-zA-Z0-9_-]*)[ \t]*\r?\n(?<command>[\s\S]*?)\r?\n```",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>The regex used to locate <c>### Title</c> + fenced-code-block pairs.</summary>
    public static Regex ExampleRegex => s_exampleRegex;

    /// <summary>
    /// Reads a <c>Name: value</c> body field including continuation lines (any non-blank,
    /// non-heading, non-fence line). Returns empty string when absent.
    /// </summary>
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

    /// <summary>Enumerates <c>### Title</c> + fenced-code-block pairs in document order. Commands use LF line endings; whitespace-only bodies are skipped.</summary>
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
            var command = match.Groups["command"].Value.ReplaceLineEndings("\n").Trim();
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

    /// <summary>Returns <see langword="true"/> when every <c>```</c> fence is paired (odd count = unterminated example).</summary>
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
