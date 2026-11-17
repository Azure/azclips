// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Network.Models;

/// <summary>
/// Hand-written flatten for the generated <c>IpConfiguration</c>, which is internal because it loops back to
/// public IP addresses. PowerShell gets the attached IP configuration's values instead. CLI JSON is unchanged.
/// </summary>
public partial class PublicIPAddress
{
    /// <summary>Resource id of the IP configuration the public IP address is attached to.</summary>
    [JsonIgnore]
    public string? IpConfigurationId => IpConfiguration?.Id;

    /// <summary>The private IP address of the attached IP configuration.</summary>
    [JsonIgnore]
    public string? PrivateIPAddress => IpConfiguration?.PrivateIPAddress;

    /// <summary>Resource id of the subnet of the attached IP configuration.</summary>
    [JsonIgnore]
    public string? SubnetId => IpConfiguration?.SubnetId;
}
