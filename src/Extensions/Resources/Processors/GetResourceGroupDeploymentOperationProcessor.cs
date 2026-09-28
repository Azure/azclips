// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Gets a single operation of a resource-group-scoped deployment by its operation id.
    /// </summary>
    public class GetResourceGroupDeploymentOperationProcessor : ResourcesProcessorBase<object, DeploymentOperation>
    {
        public string SubscriptionId { get; set; } = string.Empty;

        public string ResourceGroupName { get; set; } = string.Empty;

        public string DeploymentName { get; set; } = string.Empty;

        public string OperationId { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroupName}/deployments/{DeploymentName}/operations/{OperationId}?api-version=2026-06-01";
    }
}
