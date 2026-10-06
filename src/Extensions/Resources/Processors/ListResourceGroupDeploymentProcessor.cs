// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Atomic processor: lists resource-group-scoped deployments. ARM returns
    /// <c>{ "value": [...], "nextLink": "..." }</c>; every page is drained and a single flat array is written.
    /// </summary>
    public class ListResourceGroupDeploymentProcessor : ResourcesProcessorBase<object, DeploymentListResult>
    {
        private const string ApiVersion = "2026-06-01";

        public string SubscriptionId { get; set; } = string.Empty;

        public string ResourceGroupName { get; set; } = string.Empty;

        /// <summary>OData filter expression, e.g. <c>provisioningState eq 'Succeeded'</c>.</summary>
        public string? Filter { get; set; }

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri()
        {
            string uri = $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourcegroups/{Uri.EscapeDataString(ResourceGroupName)}/providers/Microsoft.Resources/deployments?api-version={ApiVersion}";
            return string.IsNullOrWhiteSpace(Filter) ? uri : $"{uri}&$filter={Uri.EscapeDataString(Filter)}";
        }

        private string? nextLink(DeploymentListResult body) => body.NextLink;
        private List<DeploymentExtended>? pageItems(DeploymentListResult body) => body.Value;

        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentListResult? body)
        {
            await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
            return true;
        }
    }
}
