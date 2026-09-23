// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Models;

public class PublicIPAddress
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public PublicIPAddressProperties Properties { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class PublicIPAddressProperties
{
    // Absent until the address is allocated.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IpAddress { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PublicIPAddressDnsSettings? DnsSettings { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class PublicIPAddressDnsSettings
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Fqdn { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
