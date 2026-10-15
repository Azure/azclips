// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     Information from a validate template deployment response. Returned by the
    //     deployment validate API.
    //     Note: unlike what-if — which reports a bad template as HTTP 200 with the detail
    //     in Error — ARM reports a template that fails validation as HTTP 400 carrying
    //     this same shape. Both the success and the failure payload deserialize here.
    public class DeploymentValidateResult
    {
        // The deployment validation error. Null when the template validates successfully.
        public ErrorResponse? Error { get; set; }

        // The ID of the deployment.
        public string? Id { get; set; }

        // The name of the deployment.
        public string? Name { get; set; }

        // The type of the deployment.
        public string? Type { get; set; }

        // The template deployment properties, including the validated resource list.
        public DeploymentPropertiesExtended? Properties { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
