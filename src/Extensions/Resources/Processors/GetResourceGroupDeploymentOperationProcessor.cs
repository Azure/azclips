// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Gets a single operation of a resource-group-scoped deployment
    /// (GET .../resourcegroups/{rg}/deployments/{name}/operations/{operationId}). Pure transport:
    /// the fan-out over several operation ids that <c>az deployment operation group show</c>
    /// performs lives in <see cref="GetResourceGroupDeploymentOperationProcessor_Orchestrator"/>.
    /// </summary>
    public class GetResourceGroupDeploymentOperationProcessor : ResourcesProcessorBase<object, DeploymentOperation>
    {
        private const string ApiVersion = "2026-06-01";

        public string SubscriptionId { get; set; } = string.Empty;

        public string ResourceGroupName { get; set; } = string.Empty;

        public string DeploymentName { get; set; } = string.Empty;

        public string OperationId { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Get;

        // The resource-group scope uses the legacy short path: unlike the deployment resource
        // itself, the operations sub-resource omits the providers/Microsoft.Resources segment.
        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourcegroups/{Uri.EscapeDataString(ResourceGroupName)}/deployments/{Uri.EscapeDataString(DeploymentName)}/operations/{Uri.EscapeDataString(OperationId)}?api-version={ApiVersion}";
    }
}
