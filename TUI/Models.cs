namespace Core.Tui;

/// <summary>
/// Shell mode — Azure (azv2 CLI) or Local (system shell).
/// </summary>
enum ShellMode { Azure, Local }

/// <summary>
/// History entry types — mirrors the shell UI's message taxonomy.
/// </summary>
enum HistoryItemType { User, Output, Error, Info, Success, ToolCall, Separator, Table, Verbose, Debug }

/// <summary>
/// A single history entry with type + text for styled rendering.
/// Optionally carries structured table data for rich table display.
/// </summary>
record HistoryEntry(HistoryItemType Type, string Text)
{
    /// <summary>
    /// Structured table data — set when Type is Table.
    /// </summary>
    public Rendering.TableData? TableData { get; init; }
}

/// <summary>
/// Represents an autocomplete suggestion with display text and description.
/// </summary>
record Suggestion(string Text, string Description);
