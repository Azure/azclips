// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Microsoft.Azure.Azclips.Extensions.Identity.Customization;

internal static class IdentityCliArgumentParser
{
    internal static Dictionary<string, string>? ParseTags(string[]? tags) => ParseDictionary(tags);

    //TODO: consider handling model dictionary property types in the core argument parser once more scenarios are identified.
    //for now, this is a simple helper to handle the model dictionary property type for the Identity extension.
    internal static Dictionary<string, string>? ParseDictionary(string[]? dictionary)
    {
        if (dictionary is null || dictionary.Length == 0)
            return null;

        string raw = string.Join(' ', dictionary).Trim();
        if (raw.StartsWith('{'))
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(raw);
        }

        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string entry in dictionary)
        {
            int separator = entry.IndexOf('=');
            if (separator <= 0)
            {
                throw new InvalidOperationException($"Invalid dictionary entry '{entry}'. Expected KEY=VALUE format.");
            }

            parsed[entry[..separator].Trim()] = entry[(separator + 1)..];
        }

        return parsed;
    }

    /// <summary>
    /// Parses resource restrictions
    /// example pattern is {"providers":["Microsoft.Compute"]}
    /// </summary>
    /// <param name="resourceRestriction"></param>
    /// <returns></returns>
    internal static List<string> ParseResourceRestrictionProviders(string[]? resourceRestriction)
    {
        if (resourceRestriction is null || resourceRestriction.Length == 0)
            return [];

        string raw = string.Join(' ', resourceRestriction).Trim();
        if (raw.StartsWith('{'))
        {
            using JsonDocument document = JsonDocument.Parse(raw);
            if (!document.RootElement.TryGetProperty("providers", out JsonElement providers)
                || providers.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return [.. providers.EnumerateArray()
                .Select(provider => provider.GetString())
                .Where(provider => !string.IsNullOrWhiteSpace(provider))
                .Select(provider => provider!)];
        }

        var parsed = new List<string>();
        foreach (var value in resourceRestriction)
        {
            parsed.AddRange(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return parsed;
    }
}
