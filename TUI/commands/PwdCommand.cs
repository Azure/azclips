namespace Core.Tui.Commands;

/// <summary>
/// /pwd — Show current working directory (Azure VFS path or local path).
/// </summary>
class PwdCommand : ISlashCommand
{
    public string Verb => "pwd";
    public string Description => "Show current path";

    public bool Execute(string? arg, CommandContext ctx)
    {
        ctx.AddHistory(new HistoryEntry(HistoryItemType.User, "pwd"));

        if (ctx.Mode == ShellMode.Local)
        {
            ctx.AddHistory(new HistoryEntry(HistoryItemType.Output, ctx.LocalCwd));
        }
        else
        {
            ctx.AddHistory(new HistoryEntry(HistoryItemType.Output, ctx.Vfs.CurrentPath));
            ctx.AddHistory(new HistoryEntry(HistoryItemType.Info, ctx.Vfs.GetResourceId()));
        }

        ctx.AddHistory(new HistoryEntry(HistoryItemType.Separator, ""));
        ctx.Invalidate?.Invoke();
        return true;
    }
}
