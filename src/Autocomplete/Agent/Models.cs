using System.Text.Json.Serialization;

namespace Cirrus.Autocomplete.Agent;

internal sealed class SuggestionItem
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "other";

    [JsonPropertyName("display")]
    public string? Display { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }
}

internal sealed class SuggestionResponse
{
    [JsonPropertyName("items")]
    public List<SuggestionItem> Items { get; set; } = new();
}

internal sealed class CompletionRequest
{
    [JsonPropertyName("shell")]
    public string Shell { get; set; } = "bash";

    [JsonPropertyName("line")]
    public string Line { get; set; } = string.Empty;

    [JsonPropertyName("cursor")]
    public int Cursor { get; set; }

    [JsonPropertyName("prefix")]
    public string Prefix { get; set; } = string.Empty;

    [JsonPropertyName("tokens")]
    public List<string> Tokens { get; set; } = new();

    [JsonPropertyName("completion_type")]
    public string CompletionType { get; set; } = "unknown";
}
