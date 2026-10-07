// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Network.Models;

/// <summary>
/// Result of a request to list public IP addresses. ARM list endpoints return an envelope
/// <c>{ "value": [...], "nextLink": "..." }</c>; this wraps that envelope.
/// </summary>
public class PublicIpAddressListResult
{
    /// <summary>The public IP addresses returned in this page.</summary>
    [JsonPropertyName("value")]
    public PublicIpAddress[]? Value { get; set; }

    /// <summary>The URL to use for getting the next set of results.</summary>
    [JsonPropertyName("nextLink")]
    public string? NextLink { get; set; }
}
