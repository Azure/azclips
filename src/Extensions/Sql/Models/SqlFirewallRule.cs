// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Sql.Models;

// one type serves both the PUT body and the ARM response; null members are omitted on
// write so response-only fields never leak into the request. A firewall rule is a proxy
// resource, so there is no location member.
public class SqlFirewallRule
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SqlFirewallRuleProperties? Properties { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class SqlFirewallRuleProperties
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StartIpAddress { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EndIpAddress { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
