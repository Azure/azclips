// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

/// <summary>
/// Envelope for Microsoft Graph directory collection responses such as
/// <c>GET /groups/{id}/members</c>. Graph returns
/// <c>{ "value": [...], "@odata.nextLink": "..." }</c>; this wraps that envelope.
/// </summary>
public class DirectoryObjectList
{
    public DirectoryObject[]? Value { get; set; }

    [JsonPropertyName("@odata.nextLink")]
    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
