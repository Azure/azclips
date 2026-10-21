using System.Text.Json.Serialization;

namespace Core.Agent.Tools.Resource.Cache;

/// <summary>
/// On-disk cache file envelope. Every cache JSON file under
/// <c>%LOCALAPPDATA%/.azclips/agent/arm/</c> matches this shape:
///   { schemaVersion, cachedAt, ttlSeconds, itemCount, items: [...] }
/// </summary>
public sealed class CacheFile<T>
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("cachedAt")]
    public DateTime CachedAt { get; set; }

    [JsonPropertyName("ttlSeconds")]
    public int TtlSeconds { get; set; }

    [JsonPropertyName("itemCount")]
    public int ItemCount { get; set; }

    [JsonPropertyName("items")]
    public List<T> Items { get; set; } = new();

    public bool IsExpired => DateTime.UtcNow > CachedAt.ToUniversalTime().AddSeconds(TtlSeconds);
}

public sealed record CachedTenant(
    [property: JsonPropertyName("tenantId")]      string TenantId,
    [property: JsonPropertyName("displayName")]   string? DisplayName,
    [property: JsonPropertyName("defaultDomain")] string? DefaultDomain);

public sealed record CachedSubscription(
    [property: JsonPropertyName("subscriptionId")] string SubscriptionId,
    [property: JsonPropertyName("displayName")]    string? DisplayName,
    [property: JsonPropertyName("tenantId")]       string? TenantId,
    [property: JsonPropertyName("state")]          string? State);

public sealed record CachedResourceGroup(
    [property: JsonPropertyName("id")]             string Id,
    [property: JsonPropertyName("name")]           string Name,
    [property: JsonPropertyName("location")]       string? Location,
    [property: JsonPropertyName("subscriptionId")] string SubscriptionId,
    [property: JsonPropertyName("tenantId")]       string? TenantId,
    [property: JsonPropertyName("tags")]           Dictionary<string, string>? Tags);

public sealed record CachedResource(
    [property: JsonPropertyName("id")]             string Id,
    [property: JsonPropertyName("name")]           string Name,
    [property: JsonPropertyName("type")]           string Type,
    [property: JsonPropertyName("location")]       string? Location,
    [property: JsonPropertyName("subscriptionId")] string SubscriptionId,
    [property: JsonPropertyName("tenantId")]       string? TenantId,
    [property: JsonPropertyName("resourceGroup")]  string? ResourceGroup,
    [property: JsonPropertyName("kind")]           string? Kind,
    [property: JsonPropertyName("sku")]            object? Sku,
    [property: JsonPropertyName("tags")]           Dictionary<string, string>? Tags,
    [property: JsonPropertyName("identity")]       object? Identity,
    [property: JsonPropertyName("managedBy")]      string? ManagedBy);

/// <summary>
/// Optional scope filter used by find_resources / get_resource_tree to
/// narrow ambiguous lookups when names repeat across subs/RGs.
/// </summary>
public sealed record ScopeFilter(string? Subscription, string? ResourceGroup);
