// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Azclips.Help.Tool.Validation
{
    /// <summary>
    /// Shared helpers that flag stub content left over from scaffolding (TODO markers,
    /// <c>&lt;type&gt;</c> placeholders, PlatyPS <c>{{ Fill }}</c> markers) inside help markdown.
    /// Used by both the CLI structure validator and the PowerShell example validator.
    /// </summary>
    internal static class PlaceholderDetector
    {
        // Whole-word TODO, case-insensitive.
        private static readonly Regex s_todoRegex = new(
            @"\bTODO\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Angle-bracket token: one or more non-whitespace, non-bracket chars.
        // Matches: <string>, <my-rg>. Skips: unclosed, whitespace inside, `<<`.
        private static readonly Regex s_angleBracketPlaceholderRegex = new(
            @"<[^<>\s]+>",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // PlatyPS `{{ Fill ... }}` scaffold markers (parameter descriptions, NOTES, RELATED LINKS).
        // `{{Insert list of aliases}}` is intentionally NOT matched — PlatyPS ships it as evergreen boilerplate.
        private static readonly Regex s_fillPlaceholderRegex = new(
            @"\{\{\s*Fill[^}]*\}\}",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>Returns <see langword="true"/> when <paramref name="text"/> contains a standalone TODO marker.</summary>
        public static bool ContainsTodo(string? text)
            => !string.IsNullOrEmpty(text) && s_todoRegex.IsMatch(text);

        /// <summary>Enumerates every <c>&lt;...&gt;</c> placeholder in document order; match value includes the angle brackets.</summary>
        public static IEnumerable<string> EnumerateAngleBracketPlaceholders(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            foreach (Match match in s_angleBracketPlaceholderRegex.Matches(text))
            {
                yield return match.Value;
            }
        }

        /// <summary>Enumerates every PlatyPS <c>{{ Fill ... }}</c> scaffold marker with its 1-based line number.</summary>
        public static IEnumerable<(string Token, int LineNumber)> EnumerateFillPlaceholders(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            foreach (Match match in s_fillPlaceholderRegex.Matches(text))
            {
                // Count newlines up to match.Index to compute 1-based line number.
                var lineNumber = 1;
                for (var i = 0; i < match.Index; i++)
                {
                    if (text[i] == '\n')
                    {
                        lineNumber++;
                    }
                }

                yield return (match.Value, lineNumber);
            }
        }
    }
}
