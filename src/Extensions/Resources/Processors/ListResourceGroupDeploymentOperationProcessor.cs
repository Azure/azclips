// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Atomic processor: lists all operations of a resource-group-scoped deployment, draining every page.
    /// The resource-group route has no <c>providers/Microsoft.Resources</c> segment; that matches the
    /// published specification exactly and must not be "corrected".
    /// </summary>
    public class ListResourceGroupDeploymentOperationProcessor : ResourcesProcessorBase<object, DeploymentOperationsListResult>
    {
        private const string ApiVersion = "2026-06-01";

        public string SubscriptionId { get; set; } = string.Empty;

        public string ResourceGroupName { get; set; } = string.Empty;

        public string DeploymentName { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourcegroups/{Uri.EscapeDataString(ResourceGroupName)}/deployments/{Uri.EscapeDataString(DeploymentName)}/operations?api-version={ApiVersion}";

        private string? nextLink(DeploymentOperationsListResult body) => body.NextLink;
        private IList<DeploymentOperation>? pageItems(DeploymentOperationsListResult body) => body.Value;

        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentOperationsListResult? body)
        {
            await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
            return true;
        }
    }
}
