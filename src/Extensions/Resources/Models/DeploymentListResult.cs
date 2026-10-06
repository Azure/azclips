// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     Paged collection of deployments returned by the deployment list APIs.
    public class DeploymentListResult
    {
        // The DeploymentExtended items on this page.
        public List<DeploymentExtended>? Value { get; set; }

        // The URL to use for getting the next set of results.
        public string? NextLink { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

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
