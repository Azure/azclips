using System.Globalization;
using System.Text;
using Wcwidth;

namespace Core.Agent.Hosting;

/// <summary>Result of applying one key to a <see cref="LineEditor"/>.</summary>
public enum LineEditResult
{
    /// <summary>The key edited the line or was ignored; keep reading.</summary>
    Continue,
    /// <summary>Enter: the line is complete.</summary>
    Submit,
    /// <summary>Ctrl+D or Ctrl+Z on an empty line: end of input.</summary>
    EndOfInput,
}

/// <summary>
/// Single-line editor shared by the <c>&gt; </c> prompt and the approval prompt.
/// Edits move by whole grapheme clusters and the cursor always rests on a cluster
/// boundary. The redraw measures each cluster in terminal columns and lays the line
/// out over as many rows as the terminal width needs. Rendering uses carriage
/// return and ANSI escapes only; it never reads the cursor position.
/// </summary>
public sealed class LineEditor
{
    private const int DefaultWidth = 80;
    private readonly StringBuilder _text = new();
    private readonly Func<int> _terminalWidth;
    private int _cursor;
    // Row of the terminal cursor, relative to the first row of the last render.
    private int _cursorRow;

    /// <param name="terminalWidth">Columns per terminal row; defaults to the console buffer width.</param>
    public LineEditor(Func<int>? terminalWidth = null)
    {
        _terminalWidth = terminalWidth ?? ConsoleWidth;
    }

    public string Text => _text.ToString();
    public int Cursor => _cursor;
    public bool IsEmpty => _text.Length == 0;

    /// <summary>Empties the line. The next render redraws over the rows already drawn.</summary>
    public void Clear()
    {
        _text.Clear();
        _cursor = 0;
    }

    public LineEditResult Apply(ConsoleKeyInfo key)
    {
        var alt = (key.Modifiers & ConsoleModifiers.Alt) != 0;
        var ctrl = (key.Modifiers & ConsoleModifiers.Control) != 0;
        // Alt chords are shortcuts, not text. Ctrl+Alt is AltGr and types characters.
        if (alt && !ctrl)
            return LineEditResult.Continue;
        var control = ctrl && !alt;
        if (control && key.Key is ConsoleKey.D or ConsoleKey.Z)
            return IsEmpty ? LineEditResult.EndOfInput : LineEditResult.Continue;
        // Ctrl+D / Ctrl+Z can also arrive as the raw control characters.
        if (key.KeyChar is '\x04' or '\x1a')
            return IsEmpty ? LineEditResult.EndOfInput : LineEditResult.Continue;

        switch (key.Key)
        {
            case ConsoleKey.Enter:
                return LineEditResult.Submit;
            case ConsoleKey.Backspace:
                if (_cursor > 0)
                {
                    var start = PreviousBoundary(_cursor);
                    _text.Remove(start, _cursor - start);
                    _cursor = start;
                    NormalizeCursor();
                }
                return LineEditResult.Continue;
            case ConsoleKey.Delete:
                if (_cursor < _text.Length)
                {
                    _text.Remove(_cursor, NextBoundary(_cursor) - _cursor);
                    NormalizeCursor();
                }
                return LineEditResult.Continue;
            case ConsoleKey.LeftArrow:
                if (_cursor > 0) _cursor = PreviousBoundary(_cursor);
                return LineEditResult.Continue;
            case ConsoleKey.RightArrow:
                if (_cursor < _text.Length) _cursor = NextBoundary(_cursor);
                return LineEditResult.Continue;
            case ConsoleKey.Home:
                _cursor = 0;
                return LineEditResult.Continue;
            case ConsoleKey.End:
                _cursor = _text.Length;
                return LineEditResult.Continue;
        }

        var ch = key.KeyChar;
        if (ch == '\0' || (char.IsControl(ch) && ch != '\t'))
            return LineEditResult.Continue;
        _text.Insert(_cursor, ch == '\t' ? ' ' : ch);
        _cursor++;
        // A high surrogate waits for its low surrogate before the cursor moves to a boundary.
        if (!char.IsHighSurrogate(ch))
            NormalizeCursor();
        return LineEditResult.Continue;
    }

    /// <summary>
    /// Redraws <paramref name="prompt"/> and the line from the first row of the
    /// previous render, clears whatever that render left below, and moves the
    /// cursor to the edit position.
    /// </summary>
    public void Render(TextWriter output, string prompt)
    {
        Erase(output);
        var width = Math.Max(1, _terminalWidth());
        var text = Text;
        output.Write(prompt);
        output.Write(text);
        var (endRow, endCol) = Layout(prompt + text, width);
        if (endCol == 0 && endRow > 0)
            // The last row is full; move to the next row so the position is known.
            output.Write("\r\n");
        var (row, col) = Layout(prompt + text[.._cursor], width);
        if (endRow > row)
            output.Write($"\x1b[{endRow - row}A");
        output.Write('\r');
        if (col > 0)
            output.Write($"\x1b[{col}C");
        _cursorRow = row;
        output.Flush();
    }

    /// <summary>Moves to the first row of the previous render and clears it and every row below.</summary>
    public void Erase(TextWriter output)
    {
        if (_cursorRow > 0)
            output.Write($"\x1b[{_cursorRow}A");
        output.Write("\r\x1b[J");
        _cursorRow = 0;
    }

    /// <summary>
    /// Redraws with the cursor at the end of the line and ends the row. The next
    /// render starts on a new row.
    /// </summary>
    public void Finish(TextWriter output, string prompt)
    {
        _cursor = _text.Length;
        Render(output, prompt);
        output.WriteLine();
        _cursorRow = 0;
        output.Flush();
    }

    /// <summary>Forgets the rows of the previous render, so the next render starts on the current row.</summary>
    public void Detach() => _cursorRow = 0;

    // Row and column after writing text from column 0 of row 0. A cluster that does
    // not fit on the current row starts the next one, as terminals wrap.
    private static (int Row, int Col) Layout(string text, int width)
    {
        var row = 0;
        var col = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var w = ClusterWidth(enumerator.GetTextElement());
            if (w == 0) continue;
            if (col + w > width)
            {
                row++;
                col = 0;
            }
            col += w;
            if (col >= width)
            {
                row++;
                col = 0;
            }
        }
        return (row, col);
    }

    // Moves a cursor that an edit left inside a cluster to the end of that cluster.
    private void NormalizeCursor()
    {
        var enumerator = StringInfo.GetTextElementEnumerator(Text);
        while (enumerator.MoveNext())
        {
            var start = enumerator.ElementIndex;
            if (start >= _cursor) return;
            var end = start + enumerator.GetTextElement().Length;
            if (_cursor < end)
            {
                _cursor = end;
                return;
            }
        }
    }

    private int PreviousBoundary(int index)
    {
        var boundary = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(Text);
        while (enumerator.MoveNext())
        {
            if (enumerator.ElementIndex >= index) break;
            boundary = enumerator.ElementIndex;
        }
        return boundary;
    }

    private int NextBoundary(int index)
    {
        var length = StringInfo.GetNextTextElementLength(Text.AsSpan(index));
        return index + Math.Max(1, length);
    }

    /// <summary>Terminal columns used by <paramref name="text"/>, measured per grapheme cluster.</summary>
    public static int DisplayWidth(ReadOnlySpan<char> text)
    {
        var width = 0;
        var rest = text;
        while (!rest.IsEmpty)
        {
            var length = Math.Max(1, StringInfo.GetNextTextElementLength(rest));
            width += ClusterWidth(rest[..length]);
            rest = rest[length..];
        }
        return width;
    }

    // A cluster is as wide as its base character. Emoji sequences (ZWJ, skin tone,
    // emoji presentation selector, flag pairs) are drawn as one two-column emoji.
    // Halfwidth voiced sound marks occupy their own column after the base character.
    private static int ClusterWidth(ReadOnlySpan<char> cluster)
    {
        var first = true;
        var width = 0;
        foreach (var rune in cluster.EnumerateRunes())
        {
            if (first)
            {
                width = RuneWidth(rune);
                first = false;
                continue;
            }
            if (rune.Value is 0x200D or 0xFE0F or (>= 0x1F3FB and <= 0x1F3FF) or (>= 0x1F1E6 and <= 0x1F1FF))
                return 2;
            if (rune.Value is 0xFF9E or 0xFF9F)
                width++;
        }
        return width;
    }

    private static int RuneWidth(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark
            or UnicodeCategory.Format or UnicodeCategory.Control)
            return 0;
        return Math.Max(0, UnicodeCalculator.GetWidth(rune));
    }

    private static int ConsoleWidth()
    {
        try
        {
            var width = Console.BufferWidth;
            return width > 0 ? width : DefaultWidth;
        }
        catch (IOException) { return DefaultWidth; }
        catch (PlatformNotSupportedException) { return DefaultWidth; }
    }
}
