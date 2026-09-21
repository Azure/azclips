// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Network.Models;

/// <summary>
/// Result of a request to list virtual networks. ARM list endpoints return an envelope
/// <c>{ "value": [...], "nextLink": "..." }</c>; this wraps that envelope.
/// </summary>
public class VirtualNetworkListResult
{
    /// <summary>The virtual networks returned in this page.</summary>
    [JsonPropertyName("value")]
    public VirtualNetwork[]? Value { get; set; }

    /// <summary>The URL to use for getting the next set of results.</summary>
    [JsonPropertyName("nextLink")]
    public string? NextLink { get; set; }
}
