// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     Result of the request to list role assignments. ARM returns an envelope
    //     { "value": [...], "nextLink": "..." }.
    public class RoleAssignmentListResult
    {
        [JsonPropertyName("value")]
        public RoleAssignment[]? Value { get; set; }

        //
        // Summary:
        //     The URL to use for getting the next set of results.
        [JsonPropertyName("nextLink")]
        public string? NextLink { get; set; }
    }
}
