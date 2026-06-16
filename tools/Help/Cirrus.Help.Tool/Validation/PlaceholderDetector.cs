// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Cirrus.Help.Tool.Validation
{
    /// <summary>
    /// Shared helpers that flag stub content left over from scaffolding (TODO markers,
    /// <c>&lt;type&gt;</c> placeholders) inside help markdown. Both the CLI structure
    /// validator and the PowerShell example validator call into these so the rule shape
    /// is identical across surfaces \u2014 see A1/A2 in the help-validators PR.
    /// </summary>
    internal static class PlaceholderDetector
    {
        // Matches whole-word TODO. Case-insensitive so "Todo:", "todo \u2014", and "TODO"
        // all fire. Common authoring conventions outside the help system use it as
        // a comment marker; inside our help corpus it is exclusively a stub signal.
        private static readonly Regex s_todoRegex = new(
            @"\bTODO\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Matches an angle-bracketed token of one or more non-whitespace, non-bracket chars.
        // Examples that match: <string>, <my-rg>, <subscriptionid>, <name>.
        // Examples that do NOT match: <not closed, "<x y>" (whitespace inside), `<<` etc.
        private static readonly Regex s_angleBracketPlaceholderRegex = new(
            @"<[^<>\s]+>",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Matches PlatyPS `{{ Fill ... }}` scaffold markers. The scaffolder seeds parameter
        // descriptions (`{{ Fill <Param> Description }}`), the NOTES section
        // (`{{ Fill in the Notes }}`), and RELATED LINKS
        // (`{{ Fill in the related links here }}`). All render literally in `Get-Help -Full`
        // and indicate unfinished authoring. We deliberately do NOT match
        // `{{Insert list of aliases}}` because PlatyPS emits that as evergreen ALIASES
        // boilerplate that ships unchanged in every help file in our corpus.
        private static readonly Regex s_fillPlaceholderRegex = new(
            @"\{\{\s*Fill[^}]*\}\}",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="text"/> contains a standalone
        /// TODO marker. Used to flag scaffolder stub markers in <c>summary:</c>,
        /// <c>Description:</c>, headings, and example fence bodies.
        /// </summary>
        public static bool ContainsTodo(string? text)
            => !string.IsNullOrEmpty(text) && s_todoRegex.IsMatch(text);

        /// <summary>
        /// Enumerates every <c>&lt;...&gt;</c> placeholder in <paramref name="text"/> in
        /// document order. The match value includes the angle brackets.
        /// </summary>
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

        /// <summary>
        /// Enumerates every PlatyPS <c>{{ Fill ... }}</c> scaffold marker in
        /// <paramref name="text"/> in document order, returning the matched token and
        /// the 1-based line number it appears on. Used by PSL12 to flag unfinished
        /// parameter descriptions, NOTES, and RELATED LINKS sections.
        /// </summary>
        public static IEnumerable<(string Token, int LineNumber)> EnumerateFillPlaceholders(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            foreach (Match match in s_fillPlaceholderRegex.Matches(text))
            {
                // Compute 1-based line number of the match start by counting newlines
                // up to (but not including) the match index. Avoids splitting the whole
                // file into lines just to report a position.
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
