// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;

namespace Core.Cli;

/// <summary>
/// Applies ANSI syntax-highlighting to already-serialized JSON text for terminal display.
/// Colorization is purely cosmetic — callers that need the raw JSON (e.g. for piping/parsing)
/// must gate this on <c>Core.Runtime.ColorPolicy.ShouldEmitColor</c> before calling it.
/// </summary>
internal static class JsonColorizer
{
    private const string Reset = "\u001b[0m";
    private const string KeyColor = "\u001b[36m";     // cyan
    private const string StringColor = "\u001b[32m";  // green
    private const string NumberColor = "\u001b[33m";  // yellow
    private const string LiteralColor = "\u001b[35m"; // magenta (true/false/null)

    /// <summary>
    /// Wraps object keys, string values, numbers, and true/false/null literals in <paramref
    /// name="json"/> with ANSI color codes. Walks the input with <see cref="Utf8JsonReader"/>
    /// (rather than regex) so keys and string values are distinguished by actual token type
    /// (<see cref="JsonTokenType.PropertyName"/> vs. <see cref="JsonTokenType.String"/>), and
    /// structural characters plus the original whitespace/indentation are copied through
    /// unchanged.
    /// </summary>
    internal static string Colorize(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes);
        var sb = new StringBuilder(json.Length + 64);
        long copiedTo = 0;

        while (reader.Read())
        {
            var color = reader.TokenType switch
            {
                JsonTokenType.PropertyName => KeyColor,
                JsonTokenType.String => StringColor,
                JsonTokenType.Number => NumberColor,
                JsonTokenType.True or JsonTokenType.False or JsonTokenType.Null => LiteralColor,
                _ => null,
            };
            if (color is null)
            {
                continue;
            }

            // Quoted tokens' ValueSpan excludes the surrounding quote characters.
            var isQuoted = reader.TokenType is JsonTokenType.PropertyName or JsonTokenType.String;
            var tokenStart = reader.TokenStartIndex;
            var tokenEnd = tokenStart + reader.ValueSpan.Length + (isQuoted ? 2 : 0);

            AppendSlice(sb, bytes, copiedTo, tokenStart);
            sb.Append(color);
            AppendSlice(sb, bytes, tokenStart, tokenEnd);
            sb.Append(Reset);
            copiedTo = tokenEnd;
        }

        AppendSlice(sb, bytes, copiedTo, bytes.Length);
        return sb.ToString();
    }

    private static void AppendSlice(StringBuilder sb, byte[] bytes, long start, long end)
    {
        if (end <= start)
        {
            return;
        }
        sb.Append(Encoding.UTF8.GetString(bytes, (int)start, (int)(end - start)));
    }
}
