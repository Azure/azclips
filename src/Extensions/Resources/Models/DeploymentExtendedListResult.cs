// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     Result of the request to list deployments. Contains a list of deployments.
    public class DeploymentExtendedListResult
    {
        [JsonPropertyName("value")]
        public DeploymentExtended[]? Value { get; set; }

        //
        // Summary:
        //     The URL to use for getting the next set of results.
        [JsonPropertyName("nextLink")]
        public string? NextLink { get; set; }
    }
}
