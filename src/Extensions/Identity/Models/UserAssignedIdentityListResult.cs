// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Identity.Models
{
    public class UserAssignedIdentityListResult
    {
        [JsonPropertyName("value")]
        public List<UserAssignedIdentity>? Value { get; set; }

        [JsonPropertyName("nextLink")]
        public string? NextLink { get; set; }
    }
}
