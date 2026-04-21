namespace Core.Tui;

/// <summary>
/// History entry types — mirrors the shell UI's message taxonomy.
/// </summary>
enum HistoryItemType { User, Output, Error, Info, Success, ToolCall, Separator }

/// <summary>
/// A single history entry with type + text for styled rendering.
/// </summary>
record HistoryEntry(HistoryItemType Type, string Text);
