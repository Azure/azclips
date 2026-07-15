// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Lists resource groups in a subscription. ARM list endpoints return an envelope
    /// <c>{ "value": [...], "nextLink": "..." }</c>; the unwrap + <c>nextLink</c> pagination are
    /// delegated to <see cref="ArmListPaginator"/>.
    /// </summary>
    public class ListResourceGroupProcessor : ResourcesProcessorBase<ResourceGroupListResult>
    {
        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
            string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups?api-version=2025-04-01";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }

        public override Task ProcessAsync()
        {
            EnsureRuntimeContext();
            return ArmListPaginator.CollectAndWriteAsync<ResourceGroupListResult, ResourceGroup>(
                HttpPipeline, Publisher, JsonOptions, WriteOutput, SendAsync);
        }
    }

}