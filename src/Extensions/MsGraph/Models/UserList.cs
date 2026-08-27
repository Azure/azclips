// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

/// <summary>
/// Envelope for the Microsoft Graph <c>GET /users</c> response. Graph returns
/// <c>{ "value": [...], "@odata.nextLink": "..." }</c>; this wraps that envelope.
/// </summary>
public class UserList
{
    public User[]? Value { get; set; }

    // Modeled for wire parity with Graph. Not consumed today — the current processor returns
    // the first page only. Wire the continuation loop here when pagination is added.
    [JsonPropertyName("@odata.nextLink")]
    public string? NextLink { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
