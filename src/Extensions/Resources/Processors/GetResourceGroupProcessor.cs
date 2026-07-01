// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    public class GetResourceGroupProcessor : ResourcesProcessorBase<ResourceGroup>
    {
        [ArgumentInfo(
        Required = true,
        Description = @"The name of the resource group.",
        SerializedName = @"Name")]
        public string Name { get; set; } = "";

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new InvalidOperationException("Name is required to get a resource group.");
            }

            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
            string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/{Name}?api-version=2025-04-01";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }
    }
}
