using Hex1b;
using Hex1b.Widgets;

namespace Core.Tui.Rendering;

static class HistoryRenderer
{
    public static List<Hex1bWidget> Render<TParent>(WidgetContext<TParent> ctx, List<HistoryEntry> history, TuiTheme theme)
        where TParent : Hex1bWidget
    {
        var widgets = new List<Hex1bWidget>();
        foreach (var entry in history)
        {
            switch (entry.Type)
            {
                case HistoryItemType.User:
                    widgets.Add(ctx.Text($"  {theme.AnsiUserEntry}{entry.Text}{Ansi.End}").Wrap());
                    break;
                case HistoryItemType.Output:
                    var outPre = string.IsNullOrEmpty(theme.AnsiOutput) ? "" : theme.AnsiOutput;
                    var outSuf = string.IsNullOrEmpty(theme.AnsiOutput) ? "" : Ansi.End;
                    widgets.Add(ctx.Text($"    {outPre}{entry.Text}{outSuf}").Wrap());
                    break;
                case HistoryItemType.Error:
                    widgets.Add(ctx.Text($"  {theme.AnsiError}× {entry.Text}{Ansi.End}").Wrap());
                    break;
                case HistoryItemType.Info:
                    widgets.Add(ctx.Text($"  {Ansi.AccentYellow}◇ {entry.Text}{Ansi.End}").Wrap());
                    break;
                case HistoryItemType.Success:
                    widgets.Add(ctx.Text($"  {Ansi.AccentGreen}○ {entry.Text}{Ansi.End}").Wrap());
                    break;
                case HistoryItemType.ToolCall:
                    widgets.Add(ctx.Text($"  {Ansi.AccentCyan}◦ {Ansi.Comment}{entry.Text}{Ansi.End}").Wrap());
                    break;
                case HistoryItemType.Separator:
                    widgets.Add(ctx.Text(""));
                    break;
            }
        }
        return widgets;
    }
}
