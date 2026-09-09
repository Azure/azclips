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

    // Expressed as ConsoleColor (the same vocabulary ConsoleSink/ConsoleOutputAdapter use
    // elsewhere) rather than ad-hoc named constants, even though the rendering mechanism here
    // differs from ConsoleSink's set-Console.ForegroundColor-then-write-then-reset model: a JSON
    // line needs many short-lived colored spans within one already-built string, not one color
    // for the whole write, so the codes are embedded directly rather than mutating console state.
    private static readonly string KeyColor = Ansi(ConsoleColor.Cyan);
    private static readonly string StringColor = Ansi(ConsoleColor.Green);
    private static readonly string NumberColor = Ansi(ConsoleColor.Yellow);
    private static readonly string LiteralColor = Ansi(ConsoleColor.Magenta); // true/false/null

    private static string Ansi(ConsoleColor color) => color switch
    {
        ConsoleColor.Cyan => "\u001b[36m",
        ConsoleColor.Green => "\u001b[32m",
        ConsoleColor.Yellow => "\u001b[33m",
        ConsoleColor.Magenta => "\u001b[35m",
        _ => throw new ArgumentOutOfRangeException(nameof(color), color, "No ANSI mapping registered for this color."),
    };

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
