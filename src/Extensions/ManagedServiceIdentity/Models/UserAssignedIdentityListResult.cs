// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Models
{
    public class UserAssignedIdentityListResult
    {
        [JsonPropertyName("value")]
        public List<UserAssignedIdentity> Value { get; set; } = [];

        [JsonPropertyName("nextLink")]
        public string? NextLink { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
