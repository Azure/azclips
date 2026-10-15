// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Network.Models;

public class SubnetListResult
{
    public Subnet[] Value { get; set; } = [];
    public string? NextLink { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}
