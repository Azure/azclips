using Core.Tui.Commands;

namespace Core.Tui.Services;

/// <summary>
/// Centralized completion service — produces suggestions based on input, mode, history,
/// the slash-command registry, and the CommandTree.
/// Mirrors the pattern from shell/packages/core/src/tools/azure/services/completion-service.ts.
/// </summary>
class CompletionService
{
    private readonly Dictionary<string, ISlashCommand> _commands;

    public CompletionService(Dictionary<string, ISlashCommand> commands)
    {
        _commands = commands;
    }

    /// <summary>
    /// Main entry point — returns suggestions for the current input.
    /// Returns null when an async fetch was kicked off (caller should wait for callback).
    /// </summary>
    public List<Suggestion>? GetSuggestions(
        string input,
        ShellMode mode,
        List<string> history,
        CommandContext commandContext,
        Action<List<Suggestion>> onAsyncResult)
    {
        if (string.IsNullOrWhiteSpace(input))
            return [];

        var trimmed = input.TrimStart();

        // 1. Slash commands (/cd, /ls, /pwd, /mode, /refresh)
        if (trimmed.StartsWith("/", StringComparison.OrdinalIgnoreCase))
            return GetSlashCommandSuggestions(trimmed, history);

        // 2. Slash-command verbs without prefix (e.g., "cd foo") — async VFS/local completions
        if (TryStartCommandCompletion(trimmed, commandContext, onAsyncResult))
            return null; // async — caller waits for callback

        // 3. Regular input — mode-specific
        return mode == ShellMode.Azure
            ? GetAzureModeSuggestions(input, trimmed, history)
            : GetLocalModeSuggestions(trimmed, history);
    }

    /// <summary>
    /// Accept a suggestion and produce the new InputText value.
    /// </summary>
    public string AcceptSuggestion(string inputText, string suggestionText)
    {
        var trimmed = inputText.TrimEnd();

        // Slash commands
        if (trimmed.StartsWith("/", StringComparison.OrdinalIgnoreCase))
            return AcceptSlashSuggestion(inputText, suggestionText);

        // Non-slash: partial token replacement
        if (trimmed.Length > 0 && !inputText.EndsWith(' '))
        {
            var lastSpace = inputText.LastIndexOf(' ');
            return (lastSpace >= 0 ? inputText[..(lastSpace + 1)] : "") + suggestionText + " ";
        }

        return trimmed + (trimmed.Length > 0 ? " " : "") + suggestionText + " ";
    }

    // ── Slash command suggestions ─────────────────────────────────────

    private List<Suggestion> GetSlashCommandSuggestions(string input, List<string> history)
    {
        var partial = input[(input.IndexOf('/') + 1)..].TrimStart();

        // Check if a completion-capable command has a path argument
        foreach (var cmd in _commands.Values)
        {
            if (!cmd.SupportsCompletion) continue;
            if (!partial.StartsWith(cmd.Verb + " ", StringComparison.OrdinalIgnoreCase)) continue;

            // This will be handled via async fetch — return null to signal async
            return null!; // caller detects null from outer method
        }

        // History + system suggestions
        var historySuggestions = GetHistorySuggestions(history, "/", input);

        var allCommands = _commands.Values
            .Select(c => new Suggestion(c.Verb, c.Description))
            .ToArray();

        var filtered = string.IsNullOrEmpty(partial)
            ? allCommands.ToList()
            : allCommands
                .Where(s => s.Text.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                .ToList();

        return historySuggestions.Concat(filtered).ToList();
    }

    // ── Async slash-command path completions ──────────────────────────

    /// <summary>
    /// Kick off async completion for slash commands with path args (e.g., "/cd myR").
    /// Called from GetSuggestions when we detect a path-capable slash command.
    /// </summary>
    public bool TryStartSlashCommandCompletion(
        string input,
        CommandContext ctx,
        Action<List<Suggestion>> onResult)
    {
        var partial = input[(input.IndexOf('/') + 1)..].TrimStart();

        foreach (var cmd in _commands.Values)
        {
            if (!cmd.SupportsCompletion) continue;
            if (!partial.StartsWith(cmd.Verb + " ", StringComparison.OrdinalIgnoreCase)) continue;

            var pathPartial = partial[(cmd.Verb.Length + 1)..].TrimStart();
            _ = FetchCommandCompletionsAsync(cmd, pathPartial, ctx, onResult);
            return true;
        }
        return false;
    }

    // ── Non-slash command completions (cd foo, ls bar) ────────────────

    private bool TryStartCommandCompletion(
        string trimmed,
        CommandContext ctx,
        Action<List<Suggestion>> onResult)
    {
        foreach (var cmd in _commands.Values)
        {
            if (!cmd.SupportsCompletion) continue;
            if (trimmed.Equals(cmd.Verb, StringComparison.OrdinalIgnoreCase)
                || !trimmed.StartsWith(cmd.Verb + " ", StringComparison.OrdinalIgnoreCase))
                continue;

            var partial = trimmed[(cmd.Verb.Length + 1)..].TrimStart();
            _ = FetchCommandCompletionsAsync(cmd, partial, ctx, onResult);
            return true;
        }
        return false;
    }

    private static async Task FetchCommandCompletionsAsync(
        ISlashCommand cmd,
        string partial,
        CommandContext ctx,
        Action<List<Suggestion>> onResult)
    {
        try
        {
            var completions = await cmd.GetCompletionsAsync(partial, ctx);
            if (completions != null)
                onResult(completions);
        }
        catch
        {
            onResult([]);
        }
    }

    // ── Azure mode suggestions ───────────────────────────────────────

    private List<Suggestion> GetAzureModeSuggestions(string rawInput, string trimmed, List<string> history)
    {
        var treeSuggestions = CommandTree.GetSuggestions(rawInput);
        var historySuggestions = GetHistorySuggestions(history, null, trimmed);
        return historySuggestions.Concat(treeSuggestions).ToList();
    }

    // ── Local mode suggestions ───────────────────────────────────────

    private List<Suggestion> GetLocalModeSuggestions(string trimmed, List<string> history)
    {
        var historySuggestions = GetHistorySuggestions(history, null, trimmed);

        if (trimmed.StartsWith("azv2 ", StringComparison.OrdinalIgnoreCase))
        {
            // Strip "azv2 " and feed remainder to CommandTree
            var afterPrefix = trimmed[5..];
            var treeSuggestions = CommandTree.GetSuggestions(afterPrefix);
            return historySuggestions.Concat(treeSuggestions).ToList();
        }

        if ("azv2".StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
        {
            var azv2Suggestion = new List<Suggestion> { new("azv2", "Cirrus CLI") };
            return historySuggestions.Concat(azv2Suggestion).ToList();
        }

        return historySuggestions;
    }

    // ── History suggestions ──────────────────────────────────────────

    private static List<Suggestion> GetHistorySuggestions(List<string> history, string? prefix, string partialInput)
    {
        var suggestions = new List<Suggestion>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var count = 0;
        const int max = 5;

        for (int i = history.Count - 1; i >= 0 && count < max; i--)
        {
            var cmd = history[i];

            if (prefix == "/" && !cmd.StartsWith("/", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!cmd.StartsWith(partialInput, StringComparison.OrdinalIgnoreCase))
                continue;
            if (cmd.Equals(partialInput, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!seen.Add(cmd))
                continue;

            suggestions.Add(new Suggestion(cmd, "🕐 recent"));
            count++;
        }

        return suggestions;
    }

    // ── Accept helpers ───────────────────────────────────────────────

    private static string AcceptSlashSuggestion(string inputText, string suggestion)
    {
        if (suggestion.StartsWith("/", StringComparison.OrdinalIgnoreCase))
        {
            // History suggestion is a complete command (e.g., "/cd myRG")
            return suggestion + " ";
        }

        // Use original input to detect verb + path
        var afterSlash = inputText.TrimStart()[1..];
        var spaceIdx = afterSlash.IndexOf(' ');
        if (spaceIdx >= 0)
        {
            var verb = afterSlash[..spaceIdx].TrimStart();
            return "/" + verb + " " + suggestion + " ";
        }

        // Input is just "/" or "/c" -> replace verb
        return "/" + suggestion + " ";
    }
}
