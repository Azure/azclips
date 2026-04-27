namespace Core.Tui.Commands;

/// <summary>
/// /mode — Toggle between Azure and Local shell modes.
/// </summary>
class ModeCommand : ISlashCommand
{
    public string Verb => "mode";
    public string Description => "Toggle Azure/Local mode";

    private readonly Action _toggleMode;
    private readonly Func<ShellMode> _getMode;

    public ModeCommand(Action toggleMode, Func<ShellMode> getMode)
    {
        _toggleMode = toggleMode;
        _getMode = getMode;
    }

    public bool Execute(string? arg, CommandContext ctx)
    {
        _toggleMode();
        ctx.History.Add(new HistoryEntry(HistoryItemType.Info, $"Switched to {_getMode()} mode"));
        ctx.History.Add(new HistoryEntry(HistoryItemType.Separator, ""));
        ctx.Invalidate?.Invoke();
        return true;
    }
}
