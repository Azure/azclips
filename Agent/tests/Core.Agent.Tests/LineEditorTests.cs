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
        foreach (var ch in "\U0001F468‍\U0001F469‍\U0001F467") editor.Apply(Keys.Char(ch));
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
        Assert.Equal("\r> ab\x1b[K", output.ToString());
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
        var editor = Typed("中文");
        editor.Apply(Keys.Home);
        var output = new StringWriter();
        editor.Render(output, "> ");
        Assert.EndsWith("\x1b[4D", output.ToString());
    }
}
