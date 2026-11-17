// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

internal static class VmTagsParser
{
    internal static Dictionary<string, string>? ParseTags(string[]? tags) => ParseDictionary(tags);

    private static Dictionary<string, string>? ParseDictionary(string[]? dictionary)
    {
        if (dictionary is null || dictionary.Length == 0)
            return null;

        string raw = string.Join(' ', dictionary).Trim();
        if (raw.StartsWith('{'))
        {
            var parsedJson = JsonSerializer.Deserialize<Dictionary<string, string>>(raw);
            if (parsedJson is not null && parsedJson.Keys.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException("Invalid dictionary entry. Keys cannot be empty.");
            }

            return parsedJson;
        }

        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string entry in dictionary)
        {
            int separator = entry.IndexOf('=');
            if (separator < 0)
            {
                throw new InvalidOperationException($"Invalid dictionary entry '{entry}'. Expected KEY=VALUE format.");
            }

            string key = entry[..separator].Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException($"Invalid dictionary entry '{entry}'. Keys cannot be empty.");
            }

            parsed[key] = entry[(separator + 1)..];
        }

        return parsed;
    }
}
