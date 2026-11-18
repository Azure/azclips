using Core.Agent.Hosting;
using Xunit;

namespace Core.Agent.Tests;

public class LineEditorTests
{
    private static LineEditor Typed(string text)
    {
        var editor = new LineEditor();
        foreach (var ch in text) editor.Apply(Keys.Char(ch));
        return editor;
    }

    [Fact]
    public void Backspace_RemovesAWholeGrapheme()
    {
        var editor = Typed("a");
        // Family emoji: several code points joined into one grapheme.
        foreach (var ch in "\U0001F468\u200D\U0001F469\u200D\U0001F467") editor.Apply(Keys.Char(ch));
        editor.Apply(Keys.Backspace);
        Assert.Equal("a", editor.Text);
    }

    [Fact]
    public void Backspace_RedrawsTheLineWithoutTheRemovedChar()
    {
        var editor = Typed("abc");
        editor.Apply(Keys.Backspace);
        var output = new StringWriter();
        editor.Render(output, "> ");
        Assert.Equal("\r\x1b[J> ab\r\x1b[4C", output.ToString());
    }

    [Fact]
    public void LeftRight_MoveTheInsertionPoint()
    {
        var editor = Typed("ac");
        editor.Apply(Keys.Left);
        editor.Apply(Keys.Char('b'));
        Assert.Equal("abc", editor.Text);
        editor.Apply(Keys.Left);
        editor.Apply(Keys.Right);
        editor.Apply(Keys.Char('X'));
        Assert.Equal("abXc", editor.Text);
    }

    [Fact]
    public void HomeEnd_JumpToTheEdges()
    {
        var editor = Typed("bc");
        editor.Apply(Keys.Home);
        editor.Apply(Keys.Char('a'));
        editor.Apply(Keys.End);
        editor.Apply(Keys.Char('d'));
        Assert.Equal("abcd", editor.Text);
    }

    [Fact]
    public void CtrlDAndCtrlZ_OnEmptyLine_AreEndOfInput_OtherwiseIgnored()
    {
        Assert.Equal(LineEditResult.EndOfInput, new LineEditor().Apply(Keys.CtrlD));
        Assert.Equal(LineEditResult.EndOfInput, new LineEditor().Apply(Keys.CtrlZ));
        var editor = Typed("a");
        Assert.Equal(LineEditResult.Continue, editor.Apply(Keys.CtrlD));
        Assert.Equal(LineEditResult.Continue, editor.Apply(Keys.CtrlZ));
        Assert.Equal("a", editor.Text);
    }

    [Fact]
    public void Enter_Submits()
    {
        Assert.Equal(LineEditResult.Submit, Typed("y").Apply(Keys.Enter));
    }

    [Fact]
    public void CjkCharacters_UseTwoColumns_InTheRedraw()
    {
        Assert.Equal(4, LineEditor.DisplayWidth("中文"));
        Assert.Equal(3, LineEditor.DisplayWidth("a中"));
        var editor = new LineEditor(() => 80);
        foreach (var ch in "中文") editor.Apply(Keys.Char(ch));
        editor.Apply(Keys.Home);
        var output = new StringWriter();
        editor.Render(output, "> ");
        Assert.EndsWith("\r\x1b[2C", output.ToString());
        editor.Apply(Keys.End);
        var end = new StringWriter();
        editor.Render(end, "> ");
        Assert.EndsWith("\r\x1b[6C", end.ToString());
    }

    private static void TypeChars(LineEditor editor, string text)
    {
        foreach (var ch in text) editor.Apply(Keys.Char(ch));
    }

    [Fact]
    public void RightArrow_StepsOverAWholeGrapheme()
    {
        var editor = Typed("a");
        TypeChars(editor, "\U0001F468\u200D\U0001F469b");
        editor.Apply(Keys.Home);
        editor.Apply(Keys.Right);
        editor.Apply(Keys.Right);
        Assert.Equal(6, editor.Cursor);
        editor.Apply(Keys.Char('X'));
        Assert.Equal("a\U0001F468\u200D\U0001F469Xb", editor.Text);
    }

    [Fact]
    public void LeftArrow_StepsOverAWholeGrapheme()
    {
        var editor = Typed("a");
        TypeChars(editor, "\U0001F44D\U0001F3FB");
        editor.Apply(Keys.Left);
        Assert.Equal(1, editor.Cursor);
    }

    [Theory]
    [InlineData("\U0001F468\u200D\U0001F469\u200D\U0001F467", 2)]
    [InlineData("\U0001F44D\U0001F3FB", 2)]
    [InlineData("\U0001F680", 2)]
    [InlineData("\U0001FA90", 2)]
    [InlineData("\u26A1", 2)]
    [InlineData("\U0001F1E6\U0001F1E7", 2)]
    [InlineData("e\u0301", 1)]
    [InlineData("中a", 3)]
    [InlineData("🟰", 2)]
    [InlineData("👋🏽", 2)]
    [InlineData("中文", 4)]
    [InlineData("ｶﾞﾊﾟ", 4)]
    [InlineData("界ﾞ", 3)]
    public void DisplayWidth_CountsGraphemeClusters(string text, int width) =>
        Assert.Equal(width, LineEditor.DisplayWidth(text));

    [Fact]
    public void ZwjFamily_BeforeTheCursor_TakesTwoColumns()
    {
        var editor = new LineEditor(() => 80);
        TypeChars(editor, "\U0001F468\u200D\U0001F469\u200D\U0001F467");
        var output = new StringWriter();
        editor.Render(output, "> ");
        Assert.EndsWith("\r\x1b[4C", output.ToString());
    }

    [Fact]
    public void InsertedCombiningMark_KeepsTheCursorAfterTheCluster()
    {
        var editor = Typed("ab");
        editor.Apply(Keys.Left);
        editor.Apply(Keys.Char('\u0301'));
        Assert.Equal("a\u0301b", editor.Text);
        Assert.Equal(2, editor.Cursor);
    }

    [Fact]
    public void SurrogatePairTypedBeforeALeadingCombiningMark_IsNotSplit()
    {
        var editor = new LineEditor();
        TypeChars(editor, "́");
        editor.Apply(Keys.Home);
        TypeChars(editor, "🚀");
        Assert.Equal("🚀́", editor.Text);
        Assert.Equal(3, editor.Cursor);
    }

    [Fact]
    public void InsertingARegionalIndicatorBeforeAFlag_MovesTheCursorPastTheNewPair()
    {
        var editor = new LineEditor();
        TypeChars(editor, "\U0001F1E6\U0001F1E7");
        editor.Apply(Keys.Home);
        TypeChars(editor, "\U0001F1E8");
        Assert.Equal("\U0001F1E8\U0001F1E6\U0001F1E7", editor.Text);
        Assert.Equal(4, editor.Cursor);
    }

    [Fact]
    public void DeletingASeparator_KeepsTheCursorOnAGraphemeBoundary()
    {
        var editor = new LineEditor();
        TypeChars(editor, "\U0001F1E6x\U0001F1E7");
        editor.Apply(Keys.Left);
        editor.Apply(Keys.Backspace);
        Assert.Equal(4, editor.Cursor);
        editor.Apply(Keys.Backspace);
        Assert.Equal("", editor.Text);
    }

    [Fact]
    public void AltChords_AreIgnored()
    {
        var editor = Typed("a");
        Assert.Equal(LineEditResult.Continue, editor.Apply(new ConsoleKeyInfo('b', ConsoleKey.B, false, true, false)));
        editor.Apply(new ConsoleKeyInfo('f', ConsoleKey.F, false, true, false));
        Assert.Equal("a", editor.Text);
        // Ctrl+Alt (AltGr) still types.
        editor.Apply(new ConsoleKeyInfo('@', ConsoleKey.Q, false, true, true));
        Assert.Equal("a@", editor.Text);
    }

    [Fact]
    public void WrappedDraft_RedrawMovesUpToTheFirstRowAndClearsBelow()
    {
        var editor = new LineEditor(() => 10);
        TypeChars(editor, "abcdefghijkl");
        var first = new StringWriter();
        editor.Render(first, "> ");
        // "> abcdefghijkl" is 14 columns: two rows, cursor on row 1 column 4.
        Assert.Equal("\r\x1b[J> abcdefghijkl\r\x1b[4C", first.ToString());

        editor.Apply(Keys.Backspace);
        var second = new StringWriter();
        editor.Render(second, "> ");
        Assert.StartsWith("\x1b[1A\r\x1b[J> ", second.ToString());
    }

    [Fact]
    public void WrappedDraft_CursorBeforeTheWrap_MovesUpFromTheEnd()
    {
        var editor = new LineEditor(() => 10);
        TypeChars(editor, "abcdefghijkl");
        editor.Apply(Keys.Home);
        var output = new StringWriter();
        editor.Render(output, "> ");
        Assert.EndsWith("\x1b[1A\r\x1b[2C", output.ToString());
        var next = new StringWriter();
        editor.Render(next, "> ");
        Assert.StartsWith("\r\x1b[J", next.ToString());
    }

    [Fact]
    public void ClearedWrappedDraft_IsErasedFromItsFirstRow()
    {
        var editor = new LineEditor(() => 10);
        TypeChars(editor, "abcdefghijklmnopqrst");
        editor.Render(new StringWriter(), "> ");
        editor.Clear();
        var output = new StringWriter();
        editor.Render(output, "> ");
        Assert.Equal("\x1b[2A\r\x1b[J> \r\x1b[2C", output.ToString());
    }
}
