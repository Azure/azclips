// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
            widths[c] = headers is not null ? headers[c].Length : 0;
            foreach (var row in rows)
            {
                widths[c] = Math.Max(widths[c], row[c].Length);
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

        while (constrained.Sum() > availableWidth)
        {
            var widestColumn = Array.IndexOf(constrained, constrained.Max());
            if (constrained[widestColumn] == 0)
                break;

            constrained[widestColumn]--;
        }

        return constrained;
    }

    // Pads every cell to its column's width except the last, which avoids trailing whitespace.
    private static string RenderRow(string[] cells, int[] widths, int separatorWidth) =>
        string.Join(new string(' ', separatorWidth), cells.Select((cell, i) =>
        {
            var rendered = Truncate(cell, widths[i]);
            return i == cells.Length - 1 ? rendered : rendered.PadRight(widths[i]);
        }));

    private static string Truncate(string value, int width)
    {
        if (value.Length <= width)
            return value;

        if (width <= 3)
            return value[..width];

        return value[..(width - 3)] + "...";
    }
}
