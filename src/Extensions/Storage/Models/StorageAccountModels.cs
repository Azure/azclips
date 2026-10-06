using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Models;

public class StorageAccountCreateParameters
{
    [JsonPropertyName("sku")]
    public Sku? Sku { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("tags")]
    public IDictionary<string, string>? Tags { get; set; }

    [JsonPropertyName("zones")]
    public IList<string>? Zones { get; set; }

    [JsonPropertyName("properties")]
    public StorageAccountPropertiesCreateParameters? Properties { get; set; }
}

public class StorageAccountPropertiesCreateParameters
{
    [JsonPropertyName("accessTier")]
    public string? AccessTier { get; set; }

    [JsonPropertyName("supportsHttpsTrafficOnly")]
    public bool? EnableHttpsTrafficOnly { get; set; }

    [JsonPropertyName("allowBlobPublicAccess")]
    public bool? AllowBlobPublicAccess { get; set; }

    [JsonPropertyName("minimumTlsVersion")]
    public string? MinimumTlsVersion { get; set; }

    [JsonPropertyName("publicNetworkAccess")]
    public string? PublicNetworkAccess { get; set; }

    [JsonPropertyName("isHnsEnabled")]
    public bool? IsHnsEnabled { get; set; }

    [JsonPropertyName("isSftpEnabled")]
    public bool? IsSftpEnabled { get; set; }

    [JsonPropertyName("isLocalUserEnabled")]
    public bool? IsLocalUserEnabled { get; set; }

    [JsonPropertyName("isNfsV3Enabled")]
    public bool? EnableNfsV3 { get; set; }

    [JsonPropertyName("dnsEndpointType")]
    public string? DnsEndpointType { get; set; }
}

public class StorageAccount
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("sku")]
    public Sku? Sku { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("tags")]
    public IDictionary<string, string>? Tags { get; set; }

    [JsonPropertyName("zones")]
    public IList<string>? Zones { get; set; }

    [JsonPropertyName("properties")]
    public JsonElement? Properties { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public class Sku
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("tier")]
    public string? Tier { get; set; }
}

public class StorageAccountListResult
{
    [JsonPropertyName("value")]
    public IList<StorageAccount>? Value { get; set; }

    [JsonPropertyName("nextLink")]
    public string? NextLink { get; set; }
}