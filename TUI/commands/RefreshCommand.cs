using Core.Tui.Azure;

namespace Core.Tui.Commands;

/// <summary>
/// /refresh — Refresh cached Azure data (tenants, subscriptions, resource groups, resources).
/// </summary>
class RefreshCommand : ISlashCommand
{
    public string Verb => "refresh";
    public string Description => "Refresh cache";

    public bool Execute(string? arg, CommandContext ctx)
    {
        ctx.AddHistory(new HistoryEntry(HistoryItemType.User, $"refresh {arg ?? "all"}"));

        var scope = (arg?.ToLowerInvariant()) switch
        {
            "tenants" => AzureVfs.RefreshScope.Tenants,
            "subscriptions" or "subs" => AzureVfs.RefreshScope.Subscriptions,
            "resourcegroups" or "rgs" => AzureVfs.RefreshScope.ResourceGroups,
            "resources" => AzureVfs.RefreshScope.Resources,
            _ => AzureVfs.RefreshScope.All,
        };
        ctx.Vfs.Refresh(scope);
        ctx.AddHistory(new HistoryEntry(HistoryItemType.Success, $"Cache refreshed: {scope}"));
        ctx.AddHistory(new HistoryEntry(HistoryItemType.Separator, ""));
        ctx.Invalidate?.Invoke();
        return true;
    }
}
