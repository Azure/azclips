using Core.Abstractions.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Processors
{
    public class ListResourceGroupProcessor : ResourcesListProcessorBase<ResourceGroup, ResourceGroupListResult>
    {
        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
            string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups?api-version=2025-04-01";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }
    }
}