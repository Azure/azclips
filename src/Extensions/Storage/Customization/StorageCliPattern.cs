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
                bool negate = start < characters.Length && characters[start] == '!';
                if (negate) start++;
                int end = start;
                if (end < characters.Length && characters[end] == ']') end++;
                while (end < characters.Length && characters[end] != ']') end++;
                if (end < characters.Length)
                {
                    var ranges = new List<(int, int)>();
                    while (start < end)
                    {
                        if (start + 2 < end && characters[start + 1] == '-')
                        {
                            ranges.Add((characters[start], characters[start + 2]));
                            start += 3;
                        }
                        else { ranges.Add((characters[start], characters[start])); start++; }
                    }
                    parts.Add(new Part('[', Negate: negate, Ranges: [.. ranges]));
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

    private static string Normalize(string value) =>
        OperatingSystem.IsWindows() ? value.Replace('\\', '/').ToLowerInvariant() : value;
}
