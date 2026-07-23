// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Graph.Models;

/// <summary>
/// Output of a membership check — <c>{ "value": true|false }</c>.
/// </summary>
public class GroupMembershipResult
{
    public bool Value { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
