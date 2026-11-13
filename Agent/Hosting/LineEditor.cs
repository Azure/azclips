using System.Globalization;
using System.Text;

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
/// Edits move by whole grapheme clusters and the redraw measures East Asian wide
/// characters as two columns. Rendering uses carriage return and ANSI escapes only;
/// it never reads the cursor position.
/// </summary>
public sealed class LineEditor
{
    private readonly StringBuilder _text = new();
    private int _cursor;

    public string Text => _text.ToString();
    public int Cursor => _cursor;
    public bool IsEmpty => _text.Length == 0;

    public void Clear()
    {
        _text.Clear();
        _cursor = 0;
    }

    public LineEditResult Apply(ConsoleKeyInfo key)
    {
        var control = (key.Modifiers & ConsoleModifiers.Control) != 0;
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
                }
                return LineEditResult.Continue;
            case ConsoleKey.Delete:
                if (_cursor < _text.Length)
                    _text.Remove(_cursor, NextBoundary(_cursor) - _cursor);
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
        return LineEditResult.Continue;
    }

    /// <summary>
    /// Redraws <paramref name="prompt"/> and the line on the current terminal row and
    /// moves the cursor back to the edit position.
    /// </summary>
    public void Render(TextWriter output, string prompt)
    {
        var text = Text;
        output.Write('\r');
        output.Write(prompt);
        output.Write(text);
        output.Write("\x1b[K");
        var back = DisplayWidth(text.AsSpan(_cursor));
        if (back > 0)
            output.Write($"\x1b[{back}D");
        output.Flush();
    }

    private int PreviousBoundary(int index)
    {
        var text = Text;
        var boundary = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(text);
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

    /// <summary>Terminal columns used by <paramref name="text"/>.</summary>
    public static int DisplayWidth(ReadOnlySpan<char> text)
    {
        var width = 0;
        foreach (var rune in text.EnumerateRunes())
            width += RuneWidth(rune);
        return width;
    }

    private static int RuneWidth(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark
            or UnicodeCategory.Format or UnicodeCategory.Control)
            return 0;
        return IsWide(rune.Value) ? 2 : 1;
    }

    // East Asian Wide and Fullwidth blocks.
    private static bool IsWide(int cp) =>
        (cp >= 0x1100 && cp <= 0x115F) ||
        (cp >= 0x2E80 && cp <= 0x303E) ||
        (cp >= 0x3041 && cp <= 0x33FF) ||
        (cp >= 0x3400 && cp <= 0x4DBF) ||
        (cp >= 0x4E00 && cp <= 0x9FFF) ||
        (cp >= 0xA000 && cp <= 0xA4CF) ||
        (cp >= 0xAC00 && cp <= 0xD7A3) ||
        (cp >= 0xF900 && cp <= 0xFAFF) ||
        (cp >= 0xFE30 && cp <= 0xFE4F) ||
        (cp >= 0xFF00 && cp <= 0xFF60) ||
        (cp >= 0xFFE0 && cp <= 0xFFE6) ||
        (cp >= 0x1F300 && cp <= 0x1F64F) ||
        (cp >= 0x1F900 && cp <= 0x1F9FF) ||
        (cp >= 0x20000 && cp <= 0x3FFFD);
}
