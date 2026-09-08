namespace Core.Tui;

/// <summary>
/// Shell mode — Azure (az CLI) or Local (system shell).
/// </summary>
enum ShellMode { Azure, Local }

/// <summary>
/// History entry types — mirrors the shell UI's message taxonomy.
/// </summary>
enum HistoryItemType { User, Output, Error, Warn, Info, Success, ToolCall, Separator }

/// <summary>
/// A single history entry with type + text for styled rendering.
/// </summary>
record HistoryEntry(HistoryItemType Type, string Text);

/// <summary>
/// Represents an autocomplete suggestion with display text and description.
/// </summary>
record Suggestion(string Text, string Description);
