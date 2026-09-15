// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

/// <summary>
/// Base shape for any object returned by a Microsoft Graph directory collection —
/// group members can be users, groups, service principals, applications, or devices.
/// <c>OdataType</c> distinguishes the concrete kind; type-specific fields are captured
/// verbatim in <c>AdditionalProperties</c> so callers see the full member payload.
/// </summary>
public class DirectoryObject
{
    public string? Id { get; set; }
    public string? DisplayName { get; set; }

    [JsonPropertyName("@odata.type")]
    public string? OdataType { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
