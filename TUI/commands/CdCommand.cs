using Core.Tui.Azure;

namespace Core.Tui.Commands;

/// <summary>
/// /cd — Navigate to a path in Azure VFS or local filesystem.
/// </summary>
class CdCommand : ISlashCommand
{
    public string Verb => "cd";
    public string Description => "Navigate to path";
    public bool SupportsCompletion => true;

    public bool Execute(string? arg, CommandContext ctx)
    {
        if (ctx.Mode == ShellMode.Local)
            return ExecuteLocal(arg, ctx);
        return ExecuteAzure(arg, ctx);
    }

    public async Task<List<Suggestion>?> GetCompletionsAsync(string partial, CommandContext ctx)
    {
        if (ctx.Mode == ShellMode.Azure && ctx.VfsReady)
            return await GetAzureCompletions(partial, ctx);
        if (ctx.Mode == ShellMode.Local)
            return GetLocalCompletions(partial, ctx);
        return null;
    }

    // ── Azure ──

    private static bool ExecuteAzure(string? arg, CommandContext ctx)
    {
        ctx.AddHistory(new HistoryEntry(HistoryItemType.User, $"{ctx.Prompt}/cd {arg ?? "/"}"));
        if (!ctx.VfsReady)
        {
            ctx.AddHistory(new HistoryEntry(HistoryItemType.Error, "Not authenticated. Run 'login' first."));
            ctx.AddHistory(new HistoryEntry(HistoryItemType.Separator, ""));
            ctx.Invalidate?.Invoke();
            return true;
        }
        _ = ctx.RunVfsAsync(async () =>
        {
            var error = await ctx.Vfs.CdAsync(arg ?? "/");
            if (error != null)
                ctx.AddHistory(new HistoryEntry(HistoryItemType.Error, error));
            else
                ctx.AddHistory(new HistoryEntry(HistoryItemType.Info, ctx.Vfs.CurrentPath));
            ctx.RefreshPrompt();
        });
        return true;
    }

    private static async Task<List<Suggestion>> GetAzureCompletions(string partial, CommandContext ctx)
    {
        var entries = await ctx.Vfs.LsAsync(string.IsNullOrEmpty(partial) ? null : partial);
        return entries.Select(e =>
        {
            var icon = e.Kind == VfsEntryKind.Directory ? "▸" : "·";
            return new Suggestion(e.Name, $"{icon} {e.Detail}");
        }).ToList();
    }

    // ── Local ──

    private static bool ExecuteLocal(string? arg, CommandContext ctx)
    {
        ctx.AddHistory(new HistoryEntry(HistoryItemType.User, $"{ctx.Prompt}/cd {arg ?? "~"}"));
        var target = arg;
        if (string.IsNullOrEmpty(target) || target == "~")
            target = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        else if (target == "-")
            target = ctx.LocalCwd;
        else if (target == "..")
            target = Path.GetDirectoryName(ctx.LocalCwd) ?? ctx.LocalCwd;
        else if (!Path.IsPathRooted(target))
            target = Path.GetFullPath(Path.Combine(ctx.LocalCwd, target));

        if (Directory.Exists(target))
        {
            ctx.SetLocalCwd(Path.GetFullPath(target));
            ctx.AddHistory(new HistoryEntry(HistoryItemType.Info, Path.GetFullPath(target)));
            ctx.RefreshPrompt();
        }
        else
        {
            ctx.AddHistory(new HistoryEntry(HistoryItemType.Error, $"Directory not found: {target}"));
        }
        ctx.AddHistory(new HistoryEntry(HistoryItemType.Separator, ""));
        ctx.Invalidate?.Invoke();
        return true;
    }

    private static List<Suggestion> GetLocalCompletions(string partial, CommandContext ctx)
    {
        var basePath = string.IsNullOrEmpty(partial) ? ctx.LocalCwd
            : Path.IsPathRooted(partial) ? partial
            : Path.Combine(ctx.LocalCwd, partial);

        try
        {
            string searchDir;
            string prefix;
            if (Directory.Exists(basePath))
            {
                searchDir = basePath;
                prefix = "";
            }
            else
            {
                searchDir = Path.GetDirectoryName(basePath) ?? ctx.LocalCwd;
                prefix = Path.GetFileName(basePath);
            }

            if (!Directory.Exists(searchDir))
                return [];

            var entries = new List<Suggestion>();
            foreach (var dir in Directory.GetDirectories(searchDir))
            {
                var name = Path.GetFileName(dir);
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    entries.Add(new Suggestion(name, "▸ dir"));
            }
            return entries;
        }
        catch
        {
            return [];
        }
    }
}
