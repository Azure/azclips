// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

public class User
{
    public string? Id { get; set; }
    public string? DisplayName { get; set; }
    public string? GivenName { get; set; }
    public string? Surname { get; set; }
    public string? UserPrincipalName { get; set; }
    public string? Mail { get; set; }
    public string? JobTitle { get; set; }
    public string? MobilePhone { get; set; }
    public string? OfficeLocation { get; set; }
    public string? PreferredLanguage { get; set; }
    public string[]? BusinessPhones { get; set; }
    public string? DeletedDateTime { get; set; }
    public string? UsageLocation { get; set; }
    public bool? AccountEnabled { get; set; }
    public string? MailNickname { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
