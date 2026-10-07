// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

/// <summary>
/// Envelope for the Microsoft Graph <c>GET /applications</c> response. Graph returns
/// <c>{ "value": [...], "@odata.nextLink": "..." }</c>; this wraps that envelope.
/// </summary>
public class ApplicationList
{
    public Application[]? Value { get; set; }

    // Continuation token for the next page. The list processor follows this via the shared
    // Pagination helper to return every page.
    [JsonPropertyName("@odata.nextLink")]
    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
