using System.Text.Json;

namespace Cirrus.Autocomplete.Agent;

/// <summary>
/// Parses the JSON response from the autocomplete agent into suggestion items
/// and applies basic sanitization (dedup, prefix filter, trim).
/// </summary>
internal static class SuggestionParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static List<SuggestionItem> Parse(string text, string prefix)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<SuggestionItem>();
        }

        SuggestionResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<SuggestionResponse>(text, JsonOptions);
        }
        catch (JsonException)
        {
            return new List<SuggestionItem>();
        }

        if (parsed?.Items == null)
        {
            return new List<SuggestionItem>();
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var clean = new List<SuggestionItem>(parsed.Items.Count);

        foreach (SuggestionItem item in parsed.Items)
        {
            string value = item.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(value) || value.Contains(' '))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(prefix)
                && !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!seen.Add(value))
            {
                continue;
            }

            item.Value = value;
            if (string.IsNullOrEmpty(item.Display))
            {
                item.Display = value;
            }

            clean.Add(item);
        }

        return clean;
    }
}
