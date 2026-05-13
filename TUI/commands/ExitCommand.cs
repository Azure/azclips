namespace Core.Tui.Commands;

/// <summary>
/// /exit — exits the TUI application.
/// </summary>
class ExitCommand : ISlashCommand
{
    public string Verb => "exit";
    public string Description => "Exit the application";

    public bool Execute(string? arg, CommandContext ctx)
    {
        ctx.RequestStop?.Invoke();
        return true;
    }
}
