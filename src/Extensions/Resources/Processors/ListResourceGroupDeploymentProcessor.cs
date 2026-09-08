// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Lists resource-group-scoped deployments
    /// (GET .../resourcegroups/{rg}/providers/Microsoft.Resources/deployments). ARM list endpoints
    /// return an envelope <c>{ "value": [...], "nextLink": "..." }</c>; this processor unwraps the
    /// envelope and surfaces the flat array of deployments, following <c>nextLink</c> pages.
    /// </summary>
    public class ListResourceGroupDeploymentProcessor : ResourcesProcessorBase<object, DeploymentExtendedListResult>
    {
        private const string ApiVersion = "2025-04-01";

        public string SubscriptionId { get; set; } = string.Empty;

        public string ResourceGroupName { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourcegroups/{Uri.EscapeDataString(ResourceGroupName)}/providers/Microsoft.Resources/deployments?api-version={ApiVersion}";

        private string? nextLink(DeploymentExtendedListResult body) => body.NextLink;
        private DeploymentExtended[]? pageItems(DeploymentExtendedListResult body) => body.Value;

        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentExtendedListResult? body)
        {
            await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
            return true;
        }
    }
}
