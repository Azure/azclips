// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Lists every operation performed by a resource-group-scoped deployment. ARM returns the envelope
    /// <c>{ "value": [...], "nextLink": "..." }</c>; this processor unwraps it and streams the flat array,
    /// following <c>nextLink</c> until the deployment's operations are exhausted.
    /// </summary>
    public class ListResourceGroupDeploymentOperationProcessor : ResourcesProcessorBase<object, DeploymentOperationsListResult>
    {
        public string SubscriptionId { get; set; } = string.Empty;

        public string ResourceGroupName { get; set; } = string.Empty;

        public string DeploymentName { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroupName}/deployments/{DeploymentName}/operations?api-version=2026-06-01";

        private string? nextLink(DeploymentOperationsListResult body) => body.NextLink;
        private IEnumerable<DeploymentOperation>? pageItems(DeploymentOperationsListResult body) => body.Value;

        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentOperationsListResult? body)
        {
            await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
            return true;
        }
    }
}
