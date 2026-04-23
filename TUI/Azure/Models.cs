using System.Text.Json.Serialization;

namespace Core.Tui.Azure;

// ── ARM API response wrappers ──

public record AzureTenant
{
    [JsonPropertyName("tenantId")]
    public required string TenantId { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("defaultDomain")]
    public string? DefaultDomain { get; init; }
}

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

    [JsonPropertyName("properties")]
    public ResourceGroupProperties? Properties { get; init; }
}

public record ResourceGroupProperties
{
    [JsonPropertyName("provisioningState")]
    public string? ProvisioningState { get; init; }
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

// ── ARM list response envelopes ──

public record ArmListResponse<T>
{
    [JsonPropertyName("value")]
    public required List<T> Value { get; init; }

    [JsonPropertyName("nextLink")]
    public string? NextLink { get; init; }
}
