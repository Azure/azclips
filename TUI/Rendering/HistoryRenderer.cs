using Hex1b;
using Hex1b.Widgets;

namespace Core.Tui.Rendering;

static class HistoryRenderer
{
    public static List<Hex1bWidget> Render<TParent>(WidgetContext<TParent> ctx, List<HistoryEntry> history)
        where TParent : Hex1bWidget
    {
        var widgets = new List<Hex1bWidget>();
        foreach (var entry in history)
        {
            switch (entry.Type)
            {
                case HistoryItemType.User:
                    widgets.Add(ctx.Text($"  {Ansi.AccentGreen}{entry.Text}{Ansi.End}"));
                    break;
                case HistoryItemType.Output:
                    widgets.Add(ctx.Text($"    {entry.Text}"));
                    break;
                case HistoryItemType.Error:
                    widgets.Add(ctx.Text($"  {Ansi.AccentRed}× {entry.Text}{Ansi.End}"));
                    break;
                case HistoryItemType.Info:
                    widgets.Add(ctx.Text($"  {Ansi.AccentYellow}◇ {entry.Text}{Ansi.End}"));
                    break;
                case HistoryItemType.Success:
                    widgets.Add(ctx.Text($"  {Ansi.AccentGreen}○ {entry.Text}{Ansi.End}"));
                    break;
                case HistoryItemType.ToolCall:
                    widgets.Add(ctx.Text($"  {Ansi.AccentCyan}◦ {Ansi.Comment}{entry.Text}{Ansi.End}"));
                    break;
                case HistoryItemType.Table:
                    if (entry.TableData != null)
                    {
                        var tableLines = TableRenderer.Render(entry.TableData, Console.WindowWidth);
                        foreach (var line in tableLines)
                            widgets.Add(ctx.Text(line));
                    }
                    break;
                case HistoryItemType.Separator:
                    widgets.Add(ctx.Text(""));
                    break;
            }
        }
        return widgets;
    }
}
