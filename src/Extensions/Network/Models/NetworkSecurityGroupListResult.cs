// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Network.Models;

public class NetworkSecurityGroupListResult
{
    [JsonPropertyName("value")]
    public NetworkSecurityGroup[]? Value { get; set; }

    [JsonPropertyName("nextLink")]
    public string? NextLink { get; set; }
}
