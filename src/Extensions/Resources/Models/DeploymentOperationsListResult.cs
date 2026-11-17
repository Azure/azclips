// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     Paged collection of deployment operations returned by the deployment operation list APIs.
    public class DeploymentOperationsListResult
    {
        // The DeploymentOperation items on this page.
        public List<DeploymentOperation>? Value { get; set; }

        // The URL to use for getting the next set of results.
        public string? NextLink { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
