// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace Core.Cli;

/// <summary>
/// Applies ANSI syntax-highlighting to formatted JSON text for terminal display.
/// Colorization is purely cosmetic — the underlying JSON text is unchanged aside
/// from the inserted escape sequences, so callers that need the raw JSON (e.g. for
/// piping/parsing) must gate this via <c>Core.Runtime.ColorPolicy</c> before calling it.
/// </summary>
internal static partial class JsonColorizer
{
    private const string Reset = "\u001b[0m";
    private const string KeyColor = "\u001b[36m";      // cyan
    private const string StringColor = "\u001b[32m";   // green
    private const string NumberColor = "\u001b[33m";   // yellow
    private const string LiteralColor = "\u001b[35m";  // magenta (true/false/null)

    [GeneratedRegex(
        """("(?:\\.|[^"\\])*")(\s*:)?|\b(true|false|null)\b|(-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)""")]
    private static partial Regex TokenPattern();

    /// <summary>
    /// Wraps JSON string/number/boolean/null tokens in <paramref name="json"/> with ANSI color
    /// codes; object keys (a quoted string immediately followed by <c>:</c>) get a distinct color
    /// from string values. Structural characters (braces, brackets, commas) are left uncolored.
    /// </summary>
    internal static string Colorize(string json)
    {
        return TokenPattern().Replace(json, static match =>
        {
            var stringGroup = match.Groups[1];
            if (stringGroup.Success)
            {
                var isKey = match.Groups[2].Success;
                var colon = match.Groups[2].Value;
                var color = isKey ? KeyColor : StringColor;
                return color + stringGroup.Value + Reset + colon;
            }

            if (match.Groups[3].Success)
            {
                return LiteralColor + match.Value + Reset;
            }

            if (match.Groups[4].Success)
            {
                return NumberColor + match.Value + Reset;
            }

            return match.Value;
        });
    }
}
