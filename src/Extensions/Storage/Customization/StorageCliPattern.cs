// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

internal static class StorageCliPattern
{
    private sealed record Part(int Kind, int Literal = 0, bool Negate = false, (int Start, int End)[]? Ranges = null)
    {
        internal bool Matches(int value) => Kind == '?' || (Kind == '['
            ? Ranges!.Any(range => value >= range.Start && value <= range.End) != Negate
            : Literal == value);
    }

    internal static Func<string, bool>? Compile(string? pattern)
    {
        if (pattern is null) return null;
        var characters = Normalize(pattern).EnumerateRunes().Select(rune => rune.Value).ToArray();
        var parts = new List<Part>();
        for (int index = 0; index < characters.Length; index++)
        {
            int character = characters[index];
            if (character is '*' or '?') { parts.Add(new Part(character)); continue; }
            if (character == '[')
            {
                int start = index + 1;
                int end = start;
                if (end < characters.Length && characters[end] == '!') end++;
                if (end < characters.Length && characters[end] == ']') end++;
                while (end < characters.Length && characters[end] != ']') end++;
                if (end < characters.Length)
                {
                    parts.Add(Bracket(characters, start, end));
                    index = end;
                    continue;
                }
            }
            parts.Add(new Part(0, character));
        }
        return name =>
        {
            var input = Normalize(name).EnumerateRunes().Select(rune => rune.Value).ToArray();
            int item = 0, part = 0, star = -1, restart = 0;
            while (item < input.Length)
            {
                if (part < parts.Count && parts[part].Kind == '*') { star = part++; restart = item; }
                else if (part < parts.Count && parts[part].Matches(input[item])) { part++; item++; }
                else if (star >= 0) { part = star + 1; item = ++restart; }
                else return false;
            }
            while (part < parts.Count && parts[part].Kind == '*') part++;
            return part == parts.Count;
        };
    }

    // Mirrors CPython 3.12 fnmatch.translate: split on '-' into chunks, drop descending ranges,
    // then read the remaining set like a regular expression character class.
    private static Part Bracket(int[] characters, int start, int end)
    {
        var chunks = new List<List<int>>();
        int open = start;
        int search = characters[start] == '!' ? start + 2 : start + 1;
        while (true)
        {
            if (search >= end) break;
            int hyphen = Array.IndexOf(characters, '-', search, end - search);
            if (hyphen < 0) break;
            chunks.Add(characters[open..hyphen].ToList());
            open = hyphen + 1;
            search = hyphen + 3;
        }
        if (open < end) chunks.Add(characters[open..end].ToList());
        else chunks[^1].Add('-');
        for (int k = chunks.Count - 1; k > 0; k--)
        {
            if (chunks[k - 1][^1] > chunks[k][0])
            {
                chunks[k - 1].RemoveAt(chunks[k - 1].Count - 1);
                chunks[k - 1].AddRange(chunks[k].Skip(1));
                chunks.RemoveAt(k);
            }
        }
        // Each item is (character, isRangeOperator); only hyphens joining chunks are operators.
        var items = new List<(int Value, bool Operator)>();
        for (int k = 0; k < chunks.Count; k++)
        {
            if (k > 0) items.Add(('-', true));
            items.AddRange(chunks[k].Select(value => (value, false)));
        }
        if (items.Count == 0) return new Part('[', Ranges: []);
        bool negate = !items[0].Operator && items[0].Value == '!';
        if (negate) items.RemoveAt(0);
        if (negate && items.Count == 0) return new Part('?');
        var ranges = new List<(int, int)>();
        for (int i = 0; i < items.Count; i++)
        {
            if (i + 2 < items.Count && items[i + 1].Operator)
            {
                ranges.Add((items[i].Value, items[i + 2].Value));
                i += 2;
            }
            else ranges.Add((items[i].Value, items[i].Value));
        }
        return new Part('[', Negate: negate, Ranges: [.. ranges]);
    }

    private static string Normalize(string value) =>
        OperatingSystem.IsWindows() ? value.Replace('\\', '/').ToLowerInvariant() : value;
}
