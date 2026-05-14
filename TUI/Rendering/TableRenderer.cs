namespace Core.Tui.Rendering;

/// <summary>
/// Defines a table column with header text, optional minimum width, and alignment.
/// </summary>
record TableColumn(string Header, int MinWidth = 0, TableAlignment Alignment = TableAlignment.Left);

/// <summary>
/// Holds structured table data: column definitions and row values.
/// </summary>
record TableData(TableColumn[] Columns, string[][] Rows);

/// <summary>
/// Renders structured tabular data as ANSI-styled text lines using box-drawing characters.
/// Supports auto-sizing columns, alignment, header highlighting, and terminal width capping.
/// </summary>
static class TableRenderer
{
    /// <summary>
    /// Render a table to a list of ANSI-formatted strings, suitable for history output or widget text.
    /// </summary>
    /// <param name="table">The table data to render.</param>
    /// <param name="maxWidth">Max terminal width (0 = no limit). Columns are truncated to fit.</param>
    /// <param name="indent">Left margin spaces.</param>
    public static List<string> Render(TableData table, int maxWidth = 0, int indent = 4)
    {
        if (table.Columns.Length == 0)
            return [];

        var colCount = table.Columns.Length;
        var colWidths = ComputeColumnWidths(table, colCount, maxWidth, indent);
        var alignments = table.Columns.Select(c => c.Alignment).ToArray();
        var borderColor = Ansi.Comment;
        var prefix = new string(' ', indent);
        var lines = new List<string>();

        // Top border
        lines.Add($"{prefix}{Ansi.TableTop(colWidths, borderColor)}");

        // Header row
        var headers = table.Columns.Select((c, i) => $"{Ansi.Bold}{Ansi.Foreground}{Truncate(c.Header, colWidths[i])}{Ansi.End}").ToArray();
        lines.Add($"{prefix}{Ansi.TableRow(headers, colWidths, borderColor)}");

        // Header separator
        lines.Add($"{prefix}{Ansi.TableSeparator(colWidths, borderColor)}");

        // Data rows
        foreach (var row in table.Rows)
        {
            var cells = new string[colCount];
            for (int i = 0; i < colCount; i++)
            {
                var value = i < row.Length ? row[i] ?? "" : "";
                cells[i] = Truncate(value, colWidths[i]);
            }
            lines.Add($"{prefix}{Ansi.TableRow(cells, colWidths, borderColor, Ansi.Foreground, alignments)}");
        }

        // Bottom border
        lines.Add($"{prefix}{Ansi.TableBottom(colWidths, borderColor)}");

        // Row count summary
        var countText = table.Rows.Length == 1 ? "1 row" : $"{table.Rows.Length} rows";
        lines.Add($"{prefix}{Ansi.Comment}({countText}){Ansi.End}");

        return lines;
    }

    /// <summary>
    /// Render a lightweight borderless table (compact style, no box characters).
    /// Headers are underlined; columns are separated by spaces.
    /// </summary>
    public static List<string> RenderCompact(TableData table, int maxWidth = 0, int indent = 4)
    {
        if (table.Columns.Length == 0)
            return [];

        var colCount = table.Columns.Length;
        var colWidths = ComputeColumnWidths(table, colCount, maxWidth, indent);
        var prefix = new string(' ', indent);
        var lines = new List<string>();

        // Header row
        var headerParts = new List<string>();
        for (int i = 0; i < colCount; i++)
        {
            var header = Truncate(table.Columns[i].Header, colWidths[i]);
            headerParts.Add($"{Ansi.Bold}{Ansi.AccentBlue}{PadCell(header, colWidths[i], table.Columns[i].Alignment)}{Ansi.End}");
        }
        lines.Add($"{prefix}{string.Join($"  ", headerParts)}");

        // Underline
        var underlineParts = colWidths.Select(w => $"{Ansi.Comment}{new string('─', w)}{Ansi.End}");
        lines.Add($"{prefix}{string.Join($"  ", underlineParts)}");

        // Data rows
        foreach (var row in table.Rows)
        {
            var parts = new List<string>();
            for (int i = 0; i < colCount; i++)
            {
                var value = i < row.Length ? row[i] ?? "" : "";
                var truncated = Truncate(value, colWidths[i]);
                parts.Add($"{Ansi.Foreground}{PadCell(truncated, colWidths[i], table.Columns[i].Alignment)}{Ansi.End}");
            }
            lines.Add($"{prefix}{string.Join($"  ", parts)}");
        }

        // Row count
        var countText = table.Rows.Length == 1 ? "1 row" : $"{table.Rows.Length} rows";
        lines.Add($"{prefix}{Ansi.Comment}({countText}){Ansi.End}");

        return lines;
    }

    static int[] ComputeColumnWidths(TableData table, int colCount, int maxWidth, int indent)
    {
        var colWidths = new int[colCount];

        // Start with header widths and column minimums
        for (int i = 0; i < colCount; i++)
            colWidths[i] = Math.Max(table.Columns[i].MinWidth, table.Columns[i].Header.Length);

        // Expand to fit cell content
        foreach (var row in table.Rows)
        {
            for (int i = 0; i < colCount && i < row.Length; i++)
            {
                var visible = Ansi.StripAnsiEscapes(row[i] ?? "");
                colWidths[i] = Math.Max(colWidths[i], visible.Length);
            }
        }

        // Cap columns if maxWidth is specified
        if (maxWidth > 0)
        {
            // Total width: indent + borders (colCount+1) + padding (colCount*2) + content
            var overhead = indent + colCount + 1 + colCount * 2;
            var available = maxWidth - overhead;
            if (available > 0)
            {
                var totalContent = colWidths.Sum();
                if (totalContent > available)
                {
                    // Proportionally shrink columns, keeping a minimum of 3 chars
                    var ratio = (double)available / totalContent;
                    for (int i = 0; i < colCount; i++)
                        colWidths[i] = Math.Max(3, (int)(colWidths[i] * ratio));
                }
            }
        }

        return colWidths;
    }

    static string Truncate(string text, int maxLen)
    {
        var visible = Ansi.StripAnsiEscapes(text);
        if (visible.Length <= maxLen) return text;
        // If text has no ANSI codes, simple truncation
        if (visible.Length == text.Length)
            return text[..(maxLen - 1)] + "…";
        // For ANSI-coded text, truncate the visible portion
        return visible[..(maxLen - 1)] + "…";
    }

    static string PadCell(string text, int width, TableAlignment alignment)
    {
        var visible = Ansi.StripAnsiEscapes(text);
        var pad = Math.Max(0, width - visible.Length);
        return alignment switch
        {
            TableAlignment.Right => new string(' ', pad) + text,
            TableAlignment.Center => new string(' ', pad / 2) + text + new string(' ', pad - pad / 2),
            _ => text + new string(' ', pad),
        };
    }
}
