// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

public class Group
{
    public string? Id { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? DeletedDateTime { get; set; }
    public bool? SecurityEnabled { get; set; }
    public bool? MailEnabled { get; set; }
    public string? MailNickname { get; set; }
    public string? Mail { get; set; }
    public string? Visibility { get; set; }
    public string? CreatedDateTime { get; set; }
    public string[]? GroupTypes { get; set; }
    public string[]? ProxyAddresses { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
