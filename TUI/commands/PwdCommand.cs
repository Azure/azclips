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
        ctx.History.Add(new HistoryEntry(HistoryItemType.User, "pwd"));

        if (ctx.Mode == ShellMode.Local)
        {
            ctx.History.Add(new HistoryEntry(HistoryItemType.Output, ctx.LocalCwd));
        }
        else
        {
            ctx.History.Add(new HistoryEntry(HistoryItemType.Output, ctx.Vfs.CurrentPath));
            ctx.History.Add(new HistoryEntry(HistoryItemType.Info, ctx.Vfs.GetResourceId()));
        }

        ctx.History.Add(new HistoryEntry(HistoryItemType.Separator, ""));
        ctx.Invalidate?.Invoke();
        return true;
    }
}
