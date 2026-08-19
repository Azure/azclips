using System.Text.RegularExpressions;
using Core.Tui.Services;

namespace Core.Tui.Commands;

/// <summary>
/// /copy [n] — copy last n lines of output to system clipboard (default: all from last command).
/// </summary>
class CopyCommand : ISlashCommand
{
    public string Verb => "copy";
    public string Description => "Copy output to clipboard (/copy [n])";

    public bool Execute(string? arg, CommandContext ctx)
    {
        // Determine how many output lines to copy
        int? requestedLines = null;
        if (!string.IsNullOrWhiteSpace(arg) && int.TryParse(arg.Trim(), out var n) && n > 0)
            requestedLines = n;

        // Collect output lines from the last command (back to last User entry)
        var lines = new List<string>();
        var snapshot = ctx.GetHistorySnapshot();
        for (int i = snapshot.Count - 1; i >= 0; i--)
        {
            var entry = snapshot[i];
            if (entry.Type == HistoryItemType.User || entry.Type == HistoryItemType.Separator)
            {
                if (lines.Count > 0) break; // found the boundary
                continue;
            }
            if (entry.Type == HistoryItemType.Output || entry.Type == HistoryItemType.Error
                || entry.Type == HistoryItemType.Warn
                || entry.Type == HistoryItemType.Info || entry.Type == HistoryItemType.Success)
            {
                // Strip ANSI escape codes
                var plain = Regex.Replace(entry.Text, @"\x1b\[[0-9;]*m", "");
                if (!string.IsNullOrWhiteSpace(plain))
                    lines.Add(plain);
            }
        }

        lines.Reverse();

        if (requestedLines.HasValue)
            lines = lines.TakeLast(requestedLines.Value).ToList();

        if (lines.Count == 0)
        {
            ctx.AddHistory(new HistoryEntry(HistoryItemType.Info, "Nothing to copy."));
            ctx.AddHistory(new HistoryEntry(HistoryItemType.Separator, ""));
            ctx.Invalidate?.Invoke();
            return true;
        }

        var text = string.Join(Environment.NewLine, lines);
        ClipboardService.SetText(text);

        var preview = lines.Count == 1
            ? (text.Length > 60 ? text[..57] + "..." : text)
            : $"{lines.Count} lines";
        ctx.AddHistory(new HistoryEntry(HistoryItemType.Success, $"Copied to clipboard: {preview}"));
        ctx.AddHistory(new HistoryEntry(HistoryItemType.Separator, ""));
        ctx.Invalidate?.Invoke();
        return true;
    }
}
