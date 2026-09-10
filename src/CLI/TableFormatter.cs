// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Core.Cli;

/// <summary>
/// Renders serialized JSON as a column-aligned table (classic <c>az -o table</c> shape).
/// Array-of-objects becomes multi-column with headers inferred from the first item's own
/// properties; a single object becomes a 2-column key/value table; anything else (array of
/// scalars, mixed/complex array items) becomes a single unheaded column.
/// </summary>
internal static class TableFormatter
{
    internal static string Format(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return root.ValueKind switch
        {
            JsonValueKind.Array => FormatArray(root),
            JsonValueKind.Object => FormatObject(root),
            _ => JsonCellRenderer.Render(root),
        };
    }

    private static string FormatArray(JsonElement array)
    {
        var items = array.EnumerateArray().ToList();
        if (items.Count == 0)
        {
            return string.Empty;
        }

        if (items[0].ValueKind == JsonValueKind.Object)
        {
            var headers = items[0].EnumerateObject().Select(p => p.Name).ToArray();
            var rows = items
                .Select(item => headers.Select(h => item.TryGetProperty(h, out var v) ? JsonCellRenderer.Render(v) : string.Empty).ToArray())
                .ToList();
            return RenderColumns(rows, headers);
        }

        // Array of scalars (or mixed/complex items): single column, no header.
        return RenderColumns(items.Select(item => new[] { JsonCellRenderer.Render(item) }).ToList(), headers: null);
    }

    private static string FormatObject(JsonElement obj)
    {
        // Single object: 2-column key/value table, one row per property.
        var rows = obj.EnumerateObject().Select(p => new[] { p.Name, JsonCellRenderer.Render(p.Value) }).ToList();
        return RenderColumns(rows, headers: null);
    }

    private static string RenderColumns(List<string[]> rows, string[]? headers)
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

        var lines = new List<string>();
        if (headers is not null)
        {
            lines.Add(RenderRow(headers, widths));
            lines.Add(string.Join("  ", widths.Select(w => new string('-', w))));
        }
        lines.AddRange(rows.Select(row => RenderRow(row, widths)));
        return string.Join("\n", lines);
    }

    // Pads every cell to its column's width except the last, which is left untrimmed to avoid
    // trailing whitespace on each line.
    private static string RenderRow(string[] cells, int[] widths) =>
        string.Join("  ", cells.Select((cell, i) => i == cells.Length - 1 ? cell : cell.PadRight(widths[i])));
}
