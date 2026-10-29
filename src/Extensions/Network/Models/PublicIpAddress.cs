// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Network.Models;

public class PublicIpAddress
{
    // Set by ARM on responses; null when this model is used as a create request body.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Etag { get; set; }

    public PublicIpAddressProperties Properties { get; set; } = null!;
    public string Location { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PublicIpAddressSku? Sku { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Zones { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Tags { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExtendedLocation? ExtendedLocation { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary>
/// The public IP address SKU. <c>Name</c> is <c>Basic</c>, <c>Standard</c> or <c>StandardV2</c>;
/// <c>Tier</c> is <c>Regional</c> or <c>Global</c>.
/// </summary>
public class PublicIpAddressSku
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tier { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class PublicIpAddressProperties
{
    // The wire names keep the legacy `IP` acronym casing, which the camelCase naming policy would
    // not produce from these property names, so they are pinned explicitly.
    [JsonPropertyName("publicIPAllocationMethod")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PublicIpAllocationMethod { get; set; }

    [JsonPropertyName("publicIPAddressVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PublicIpAddressVersion { get; set; }

    [JsonPropertyName("publicIPPrefix")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubResource? PublicIpPrefix { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PublicIpAddressDnsSettings? DnsSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DdosSettings? DdosSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<IpTag>? IpTags { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IpAddress { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? IdleTimeoutInMinutes { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DeleteOption { get; set; }

    // Read-only. ARM returns the full IP configuration envelope; kept as an opaque node so
    // `show` echoes it verbatim instead of flattening it to an id. Type it when a command reads into it.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? IpConfiguration { get; set; }

    // Response-only references the spec declares on the resource. ARM returns them as `{ "id": ... }`,
    // so they are typed as SubResource to survive `show`/`list` and the PowerShell formatter.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubResource? NatGateway { get; set; }

    [JsonPropertyName("servicePublicIPAddress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubResource? ServicePublicIpAddress { get; set; }

    [JsonPropertyName("linkedPublicIPAddress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubResource? LinkedPublicIpAddress { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MigrationPhase { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResourceGuid { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProvisioningState { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary>DNS record settings for the public IP. <c>Fqdn</c> is read-only.</summary>
public class PublicIpAddressDnsSettings
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DomainNameLabel { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DomainNameLabelScope { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Fqdn { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReverseFqdn { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary>DDoS protection settings. <c>ProtectionMode</c> is one of the <c>DdosSettingsProtectionMode</c> values.</summary>
public class DdosSettings
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProtectionMode { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubResource? DdosCustomPolicy { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubResource? DdosProtectionPlan { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary>An IP tag, e.g. <c>ipTagType=FirstPartyUsage</c> with <c>tag=/Sql</c>.</summary>
public class IpTag
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IpTagType { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tag { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
