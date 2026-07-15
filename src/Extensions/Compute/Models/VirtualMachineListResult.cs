// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Models;

/// <summary>
/// Result of a request to list virtual machines. ARM list endpoints return an envelope
/// <c>{ "value": [...], "nextLink": "..." }</c>; this wraps that envelope.
/// </summary>
public class VirtualMachineListResult
{
    /// <summary>The virtual machines returned in this page.</summary>
    [JsonPropertyName("value")]
    public VirtualMachine[]? Value { get; set; }

    /// <summary>The URL to use for getting the next set of results.</summary>
    [JsonPropertyName("nextLink")]
    public string? NextLink { get; set; }
}
