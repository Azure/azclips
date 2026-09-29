// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Lists every operation recorded for a resource-group-scoped deployment
    /// (GET .../resourcegroups/{rg}/deployments/{name}/operations). ARM returns the envelope
    /// <c>{ "value": [...], "nextLink": "..." }</c>; this processor unwraps it and lets the runtime
    /// follow <c>nextLink</c> so the caller sees a flat stream of operations. This is the AzClips
    /// equivalent of <c>az deployment operation group list</c> /
    /// <c>Get-AzResourceGroupDeploymentOperation</c>.
    /// </summary>
    public class ListResourceGroupDeploymentOperationProcessor : ResourcesProcessorBase<object, DeploymentOperationsListResult>
    {
        private const string ApiVersion = "2026-06-01";

        public string SubscriptionId { get; set; } = string.Empty;

        public string ResourceGroupName { get; set; } = string.Empty;

        public string DeploymentName { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Get;

        // The resource-group scope uses the legacy short path: unlike the deployment resource
        // itself, the operations sub-resource omits the providers/Microsoft.Resources segment.
        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourcegroups/{Uri.EscapeDataString(ResourceGroupName)}/deployments/{Uri.EscapeDataString(DeploymentName)}/operations?api-version={ApiVersion}";

        private string? nextLink(DeploymentOperationsListResult body) => body.NextLink;
        private List<DeploymentOperation>? pageItems(DeploymentOperationsListResult body) => body.Value;

        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentOperationsListResult? body)
        {
            await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
            return true;
        }
    }
}
