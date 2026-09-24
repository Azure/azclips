// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     List of deployment operations. ARM returns this envelope from the deployment
    //     operations collection endpoint.
    public class DeploymentOperationsListResult
    {
        // The deployment operations on this page.
        public List<DeploymentOperation>? Value { get; set; }

        // The link to the next page of items.
        public string? NextLink { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
