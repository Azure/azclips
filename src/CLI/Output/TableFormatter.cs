// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Core.Cli.Output;

/// <summary>
/// Renders serialized JSON as a column-aligned table (classic <c>az -o table</c> shape).
/// Array-of-objects becomes multi-column with headers inferred from the first item's own
/// properties; a single object becomes a 2-column key/value table; anything else (array of
/// scalars, mixed/complex array items) becomes a single unheaded column.
/// </summary>
internal static class TableFormatter
{
    internal static string Format(string json, int? terminalWidth = null)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return root.ValueKind switch
        {
            JsonValueKind.Array => FormatArray(root, terminalWidth),
            JsonValueKind.Object => FormatObject(root, terminalWidth),
            _ => JsonCellRenderer.Render(root),
        };
    }

    private static string FormatArray(JsonElement array, int? terminalWidth)
    {
        var items = array.EnumerateArray().ToList();
        if (items.Count == 0)
        {
            return string.Empty;
        }

        if (items.All(item => item.ValueKind == JsonValueKind.Object))
        {
            var headers = JsonCellRenderer.GetScalarPropertyNames(items[0]);
            var rows = items
                .Select(item => headers.Select(h => item.TryGetProperty(h, out var v) ? JsonCellRenderer.Render(v) : string.Empty).ToArray())
                .ToList();
            return RenderColumns(rows, headers, terminalWidth);
        }

        // Array of scalars (or mixed/complex items): single column, no header.
        return RenderColumns(items.Select(item => new[] { JsonCellRenderer.Render(item) }).ToList(), headers: null, terminalWidth: terminalWidth);
    }

    private static string FormatObject(JsonElement obj, int? terminalWidth)
    {
        // Single object: 2-column key/value table, one row per scalar property (nested object/
        // array properties are dropped entirely, matching classic az).
        var rows = obj.EnumerateObject()
            .Where(p => p.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
            .Select(p => new[] { p.Name, JsonCellRenderer.Render(p.Value) })
            .ToList();
        return RenderColumns(rows, headers: null, terminalWidth: terminalWidth);
    }

    private static string RenderColumns(List<string[]> rows, string[]? headers, int? terminalWidth)
    {
        var columnCount = headers?.Length ?? (rows.Count > 0 ? rows[0].Length : 0);
        if (columnCount == 0)
        {
            return string.Empty;
        }

        var widths = new int[columnCount];
        for (var c = 0; c < columnCount; c++)
        {
            widths[c] = headers is not null ? GetDisplayWidth(headers[c]) : 0;
            foreach (var row in rows)
            {
                widths[c] = Math.Max(widths[c], GetDisplayWidth(row[c]));
            }
        }

        var separatorWidth = GetSeparatorWidth(columnCount, terminalWidth);
        widths = ConstrainWidths(widths, terminalWidth, separatorWidth);

        var lines = new List<string>();
        if (headers is not null)
        {
            lines.Add(RenderRow(headers, widths, separatorWidth));
            lines.Add(string.Join(new string(' ', separatorWidth), widths.Select(w => new string('-', w))));
        }
        lines.AddRange(rows.Select(row => RenderRow(row, widths, separatorWidth)));
        return string.Join("\n", lines);
    }

    private static int GetSeparatorWidth(int columnCount, int? terminalWidth)
    {
        if (terminalWidth is null or <= 0)
            return 2;

        var twoSpaceMinimum = columnCount + 2 * (columnCount - 1);
        if (terminalWidth >= twoSpaceMinimum)
            return 2;

        return terminalWidth >= columnCount ? 1 : 0;
    }

    private static int[] ConstrainWidths(int[] widths, int? terminalWidth, int separatorWidth)
    {
        if (terminalWidth is not > 0)
            return widths;

        var availableWidth = Math.Max(0, terminalWidth.Value - separatorWidth * (widths.Length - 1));
        var constrained = widths.ToArray();
        var excess = constrained.Sum() - availableWidth;

        while (excess > 0)
        {
            var widestColumn = Array.IndexOf(constrained, constrained.Max());
            if (widestColumn < 0 || constrained[widestColumn] == 0)
                break;

            var reduction = Math.Min(constrained[widestColumn], excess);
            constrained[widestColumn] -= reduction;
            excess -= reduction;
        }

        return constrained;
    }

    // Pads every cell to its column's width except the last, which avoids trailing whitespace.
    private static string RenderRow(string[] cells, int[] widths, int separatorWidth) =>
        string.Join(new string(' ', separatorWidth), cells.Select((cell, i) =>
        {
            var rendered = Truncate(cell, widths[i]);
            return i == cells.Length - 1 ? rendered : PadToDisplayWidth(rendered, widths[i]);
        }));

    private static string PadToDisplayWidth(string value, int width)
    {
        var current = GetDisplayWidth(value);
        if (current >= width)
            return value;

        return value + new string(' ', width - current);
    }

    private static string Truncate(string value, int width)
    {
        if (width <= 0)
            return string.Empty;

        if (GetDisplayWidth(value) <= width)
            return value;

        if (width <= 3)
            return new string('.', width);

        var runes = value.EnumerateRunes().ToList();
        if (runes.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        var used = 0;
        foreach (var rune in runes)
        {
            var cellWidth = GetRuneDisplayWidth(rune);
            if (cellWidth == 0)
            {
                sb.Append(rune);
                continue;
            }

            if (used + cellWidth > width - 3)
                break;

            sb.Append(rune);
            used += cellWidth;
        }

        while (GetDisplayWidth(sb.ToString()) + 3 > width && sb.Length > 0)
        {
            var lastRune = sb.ToString().EnumerateRunes().Last();
            sb.Length -= lastRune.Utf16SequenceLength;
        }

        return sb.ToString() + "...";
    }

    private static int GetDisplayWidth(string value)
    {
        var total = 0;
        var column = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Value is 0x0A or 0x0D)
            {
                continue;
            }

            if (rune.Value == '\t')
            {
                var nextTabStop = ((column / 8) + 1) * 8;
                total += nextTabStop - column;
                column = nextTabStop;
                continue;
            }

            var width = GetRuneDisplayWidth(rune);
            total += width;
            column += width;
        }

        return total;
    }

    private static int GetRuneDisplayWidth(Rune rune)
    {
        var codePoint = rune.Value;

        if (codePoint is '\t' or '\r' or '\n' or '\0')
            return 0;

        if (rune.IsAscii)
            return 1;

        if (codePoint is 0x200D or 0xFE0E or 0xFE0F)
            return 0;

        if (codePoint is >= 0x1F3FB and <= 0x1F3FF)
            return 0;

        var category = CharUnicodeInfo.GetUnicodeCategory(rune.ToString(), 0);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
            return 0;

        return codePoint switch
        {
            >= 0x1100 and <= 0x115F => 2,
            >= 0x2E80 and <= 0xA4CF => 2,
            >= 0xAC00 and <= 0xD7A3 => 2,
            >= 0xF900 and <= 0xFAFF => 2,
            >= 0xFE30 and <= 0xFE4F => 2,
            >= 0xFF00 and <= 0xFF60 => 2,
            >= 0xFFE0 and <= 0xFFE6 => 2,
            >= 0x1F300 and <= 0x1FAFF => 2,
            >= 0x2600 and <= 0x27BF => 2,
            _ => 1,
        };
    }
}
