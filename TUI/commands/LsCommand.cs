using Core.Tui.Azure;

namespace Core.Tui.Commands;

/// <summary>
/// /ls — List contents of Azure VFS or local filesystem.
/// </summary>
class LsCommand : ISlashCommand
{
    public string Verb => "ls";
    public string Description => "List contents";
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
        ctx.AddHistory(new HistoryEntry(HistoryItemType.User, $"{ctx.Prompt}/ls {arg ?? ""}".TrimEnd()));
        if (!ctx.VfsReady)
        {
            ctx.AddHistory(new HistoryEntry(HistoryItemType.Error, "Not authenticated. Run 'login' first."));
            ctx.AddHistory(new HistoryEntry(HistoryItemType.Separator, ""));
            ctx.Invalidate?.Invoke();
            return true;
        }
        _ = ctx.RunVfsAsync(async () =>
        {
            var entries = await ctx.Vfs.LsAsync(arg);
            if (entries.Count == 0)
            {
                ctx.AddHistory(new HistoryEntry(HistoryItemType.Info, "(empty)"));
            }
            else
            {
                foreach (var e in entries)
                {
                    var icon = e.Kind == VfsEntryKind.Directory ? "▸" : "·";
                    ctx.AddHistory(new HistoryEntry(HistoryItemType.Output, $"  {icon} {e.Name,-40} {e.Detail}"));
                }
                ctx.AddHistory(new HistoryEntry(HistoryItemType.Info, $"{entries.Count} items"));
            }
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
        ctx.AddHistory(new HistoryEntry(HistoryItemType.User, $"{ctx.Prompt}/ls {arg ?? ""}".TrimEnd()));
        var target = string.IsNullOrEmpty(arg) ? ctx.LocalCwd
            : Path.IsPathRooted(arg) ? arg
            : Path.GetFullPath(Path.Combine(ctx.LocalCwd, arg));

        if (!Directory.Exists(target))
        {
            ctx.AddHistory(new HistoryEntry(HistoryItemType.Error, $"Directory not found: {target}"));
            ctx.AddHistory(new HistoryEntry(HistoryItemType.Separator, ""));
            ctx.Invalidate?.Invoke();
            return true;
        }

        var count = 0;
        foreach (var dir in Directory.GetDirectories(target))
        {
            ctx.AddHistory(new HistoryEntry(HistoryItemType.Output, $"  ▸ {Path.GetFileName(dir)}/"));
            count++;
        }
        foreach (var file in Directory.GetFiles(target))
        {
            var info = new FileInfo(file);
            var size = info.Length < 1024 ? $"{info.Length}B" : $"{info.Length / 1024}KB";
            ctx.AddHistory(new HistoryEntry(HistoryItemType.Output, $"  · {Path.GetFileName(file),-40} {size}"));
            count++;
        }
        ctx.AddHistory(new HistoryEntry(HistoryItemType.Info, count == 0 ? "(empty)" : $"{count} items"));
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
            foreach (var file in Directory.GetFiles(searchDir))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    entries.Add(new Suggestion(name, "· file"));
            }
            return entries;
        }
        catch
        {
            return [];
        }
    }
}
