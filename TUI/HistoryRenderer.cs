namespace Core.Tui;

static class HistoryRenderer
{
    public static List<string> Render(List<HistoryEntry> history)
    {
        var lines = new List<string>();
        foreach (var entry in history)
        {
            switch (entry.Type)
            {
                case HistoryItemType.User:
                    lines.Add($"  {Ansi.AccentGreen}>{Ansi.End} {Ansi.Foreground}{entry.Text}{Ansi.End}");
                    break;
                case HistoryItemType.Output:
                    lines.Add($"    {entry.Text}");
                    break;
                case HistoryItemType.Error:
                    lines.Add($"  {Ansi.AccentRed}× {entry.Text}{Ansi.End}");
                    break;
                case HistoryItemType.Info:
                    lines.Add($"  {Ansi.AccentYellow}◇ {entry.Text}{Ansi.End}");
                    break;
                case HistoryItemType.Success:
                    lines.Add($"  {Ansi.AccentGreen}○ {entry.Text}{Ansi.End}");
                    break;
                case HistoryItemType.ToolCall:
                    lines.Add($"  {Ansi.AccentCyan}◦ {Ansi.Comment}{entry.Text}{Ansi.End}");
                    break;
                case HistoryItemType.Separator:
                    lines.Add("");
                    break;
            }
        }
        return lines;
    }
}
