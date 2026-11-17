// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Network.Models;

/// <summary>
/// Hand-written flatten for <see cref="IPConfiguration"/>. Its subnet and public IP references loop back to
/// IP configurations, so codegen leaves it unflattened (<c>noInline</c> in aceconfig.yaml). Exposes the values
/// <see cref="PublicIPAddress"/> flattens from its attached IP configuration; the nested objects stay hidden.
/// CLI JSON is unchanged.
/// </summary>
public partial class IPConfiguration
{
    /// <summary>The private IP address of the IP configuration.</summary>
    [JsonIgnore]
    public string? PrivateIPAddress => Properties?.PrivateIPAddress;

    /// <summary>Resource id of the subnet.</summary>
    [JsonIgnore]
    public string? SubnetId => Properties?.SubnetId;
}
