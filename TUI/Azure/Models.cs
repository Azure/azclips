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

/// <summary>
/// In-memory tree of all Azure resources fetched in a single ARG query.
/// Organized as: Subscriptions → ResourceGroups → Resources.
/// </summary>
public class AzureResourceTree
{
    public List<AzureSubscription> Subscriptions { get; } = [];

    /// <summary>Resource groups keyed by subscriptionId.</summary>
    public Dictionary<string, List<AzureResourceGroup>> ResourceGroups { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Resources keyed by "subscriptionId/resourceGroupName".</summary>
    public Dictionary<string, List<AzureResource>> Resources { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<AzureResourceGroup> GetResourceGroups(string subscriptionId)
        => ResourceGroups.TryGetValue(subscriptionId, out var rgs) ? rgs : [];

    public List<AzureResource> GetResources(string subscriptionId, string resourceGroupName)
        => Resources.TryGetValue($"{subscriptionId}/{resourceGroupName}", out var res) ? res : [];
}
