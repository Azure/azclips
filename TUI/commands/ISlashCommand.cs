using Core.Tui.Azure;

namespace Core.Tui.Commands;

/// <summary>
/// Context passed to slash commands for execution and completion.
/// </summary>
class CommandContext
{
    public required Action<HistoryEntry> AddHistory { get; init; }
    public required Func<List<HistoryEntry>> GetHistorySnapshot { get; init; }
    public required AzureVfs Vfs { get; init; }
    public required bool VfsReady { get; init; }
    public required ShellMode Mode { get; init; }
    public required string LocalCwd { get; init; }
    public required Action? Invalidate { get; init; }
    public required Action? RequestStop { get; init; }
    public required Action RefreshPrompt { get; init; }
    public required Action<string> SetLocalCwd { get; init; }
    public required Func<Func<Task>, Task> RunVfsAsync { get; init; }
}

/// <summary>
/// A slash command that can be executed and optionally provides completions.
/// Each command lives in its own file, similar to the TS shell pattern.
/// </summary>
interface ISlashCommand
{
    /// <summary>Command verb (e.g., "cd", "ls", "pwd").</summary>
    string Verb { get; }

    /// <summary>Short description shown in suggestion list.</summary>
    string Description { get; }

    /// <summary>
    /// Execute the command.
    /// Returns true if handled, false if unrecognized.
    /// </summary>
    bool Execute(string? arg, CommandContext ctx);

    /// <summary>
    /// Whether this command supports async path completions.
    /// </summary>
    bool SupportsCompletion => false;

    /// <summary>
    /// Get completions for this command's argument.
    /// Returns null if this command doesn't support completions.
    /// </summary>
    Task<List<Suggestion>?> GetCompletionsAsync(string partial, CommandContext ctx) =>
        Task.FromResult<List<Suggestion>?>(null);
}
