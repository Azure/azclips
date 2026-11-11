// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Lists subscription-scoped deployments
    /// (GET /subscriptions/{sub}/providers/Microsoft.Resources/deployments), unwrapping the
    /// <c>{ value, nextLink }</c> envelope and following <c>nextLink</c> pages.
    /// </summary>
    public class ListSubscriptionDeploymentProcessor : ResourcesProcessorBase<object, DeploymentExtendedListResult>
    {
        private const string ApiVersion = "2025-04-01";

        public string SubscriptionId { get; set; } = string.Empty;

        /// <summary>Optional OData <c>$filter</c>, e.g. <c>provisioningState eq 'Failed'</c>.</summary>
        public string? Filter { get; set; }

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri()
        {
            var uri = $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/providers/Microsoft.Resources/deployments?api-version={ApiVersion}";
            return string.IsNullOrWhiteSpace(Filter) ? uri : $"{uri}&$filter={Uri.EscapeDataString(Filter)}";
        }

        private string? nextLink(DeploymentExtendedListResult body) => body.NextLink;
        private DeploymentExtended[]? pageItems(DeploymentExtendedListResult body) => body.Value;

        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentExtendedListResult? body)
        {
            await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
            return true;
        }
    }
}
