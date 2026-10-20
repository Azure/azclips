// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Parses, normalizes, mutates, and matches command exclusion lists such as <c>agent.excludedCommands</c>.
/// Entries are separated by <c>,</c> or <c>;</c>. Matching is case-insensitive, ignores one leading
/// <c>az</c> token and extra whitespace, and uses token-prefix semantics (a group entry excludes its subcommands).
/// </summary>
public static class CommandExclusionList
{
    private static readonly char[] EntrySeparators = [',', ';'];
    private static readonly char[] WhitespaceSeparators = [' ', '\t', '\r', '\n'];

    public const string Separator = ",";

    /// <summary>Splits a stored list into trimmed, non-empty entries, preserving original spelling and order.</summary>
    public static IReadOnlyList<string> Split(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(EntrySeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(entry => entry.Length > 0)
            .ToArray();
    }

    /// <summary>Returns the normalized tokens of an entry: whitespace collapsed, one leading <c>az</c> dropped, lower-cased.</summary>
    public static IReadOnlyList<string> Tokenize(string? entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            return [];
        }

        var tokens = entry
            .Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.ToLowerInvariant())
            .ToList();

        if (tokens.Count > 0 && tokens[0] == "az")
        {
            tokens.RemoveAt(0);
        }

        return tokens;
    }

    /// <summary>Normalized comparison key for an entry (tokens joined with a single space).</summary>
    public static string Normalize(string? entry) => string.Join(' ', Tokenize(entry));

    /// <summary>Removes duplicates (by normalized key), keeping the first occurrence's original spelling and order.</summary>
    public static IReadOnlyList<string> Deduplicate(IEnumerable<string> entries)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var entry in entries)
        {
            var trimmed = entry.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (seen.Add(Normalize(trimmed)))
            {
                result.Add(trimmed);
            }
        }

        return result;
    }

    /// <summary>Joins entries with <c>,</c>.</summary>
    public static string Join(IEnumerable<string> entries) => string.Join(Separator, entries);

    /// <summary>Adds the entries in <paramref name="additions"/> (which may itself be a separated list) to <paramref name="current"/>.</summary>
    public static string Add(string? current, string? additions)
    {
        var combined = Split(current).Concat(Split(additions));
        return Join(Deduplicate(combined));
    }

    /// <summary>Removes the entries in <paramref name="removals"/> (which may itself be a separated list) from <paramref name="current"/>.</summary>
    public static string Remove(string? current, string? removals)
    {
        var toRemove = new HashSet<string>(Split(removals).Select(Normalize), StringComparer.Ordinal);
        var remaining = Split(current).Where(entry => !toRemove.Contains(Normalize(entry)));
        return Join(Deduplicate(remaining));
    }

    /// <summary>
    /// Returns true when any entry of <paramref name="list"/> matches <paramref name="commandTokens"/> by token prefix.
    /// When the command is not resolved, only one-token entries are compared against the first token.
    /// Entries that normalize to zero tokens never match.
    /// </summary>
    public static bool IsExcluded(string? list, IReadOnlyList<string> commandTokens, bool resolved)
    {
        var entries = Split(list);
        if (entries.Count == 0 || commandTokens is null || commandTokens.Count == 0)
        {
            return false;
        }

        var command = Tokenize(string.Join(' ', commandTokens));
        if (command.Count == 0)
        {
            return false;
        }

        foreach (var entry in entries)
        {
            var entryTokens = Tokenize(entry);
            if (entryTokens.Count == 0)
            {
                continue;
            }

            if (!resolved)
            {
                if (entryTokens.Count == 1 && string.Equals(entryTokens[0], command[0], StringComparison.Ordinal))
                {
                    return true;
                }

                continue;
            }

            if (entryTokens.Count > command.Count)
            {
                continue;
            }

            var matches = true;
            for (var index = 0; index < entryTokens.Count; index++)
            {
                if (!string.Equals(entryTokens[index], command[index], StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }
}
