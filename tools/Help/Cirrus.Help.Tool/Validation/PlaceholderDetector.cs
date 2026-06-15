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
    }
}
