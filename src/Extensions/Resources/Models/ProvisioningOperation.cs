// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     The name of the current provisioning operation recorded on a deployment operation.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ProvisioningOperation
    {
        NotSpecified,
        Create,
        Delete,
        Waiting,
        AzureAsyncOperationWaiting,
        ResourceCacheWaiting,
        Action,
        Read,
        EvaluateDeploymentOutput,
        DeploymentCleanup
    }
}
