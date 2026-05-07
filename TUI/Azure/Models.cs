using System.Text.Json.Serialization;

namespace Core.Tui.Azure;

// ── Azure resource models (deserialized from ARG responses) ──

public record AzureSubscription
{
    [JsonPropertyName("subscriptionId")]
    public required string SubscriptionId { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("tenantId")]
    public string? TenantId { get; init; }
}

public record AzureResourceGroup
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("location")]
    public required string Location { get; init; }

    [JsonPropertyName("id")]
    public string? Id { get; init; }
}

public record AzureResource
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("location")]
    public string? Location { get; init; }

    [JsonPropertyName("kind")]
    public string? Kind { get; init; }
}
