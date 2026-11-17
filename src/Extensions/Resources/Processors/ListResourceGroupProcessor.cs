// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Lists resource groups in a subscription. ARM list endpoints return an envelope
    /// <c>{ "value": [...], "nextLink": "..." }</c>; this processor unwraps the envelope and
    /// follows <c>nextLink</c> to return resource groups from every page in a single batch.
    /// </summary>
    public class ListResourceGroupProcessor : ResourcesProcessorBase<object, ResourceGroupListResult>
    {
        public string SubscriptionId { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups?api-version=2025-04-01";

        private string? nextLink(ResourceGroupListResult body) => body.NextLink;
        private ResourceGroup[]? pageItems(ResourceGroupListResult body) => body.Value;

        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(ResourceGroupListResult? body)
        {
            await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
            return true;
        }
    }
}