namespace Core.Tui.Commands;

/// <summary>
/// /refresh — Refresh cached Azure data (subscriptions, resource groups, resources).
/// </summary>
class RefreshCommand : ISlashCommand
{
    public string Verb => "refresh";
    public string Description => "Refresh cache";

    public bool Execute(string? arg, CommandContext ctx)
    {
        ctx.AddHistory(new HistoryEntry(HistoryItemType.User, $"{ctx.Prompt}/refresh"));

        ctx.Vfs.Refresh();
        ctx.AddHistory(new HistoryEntry(HistoryItemType.Success, "Cache refreshed"));
        ctx.AddHistory(new HistoryEntry(HistoryItemType.Separator, ""));
        ctx.Invalidate?.Invoke();
        return true;
    }
}
