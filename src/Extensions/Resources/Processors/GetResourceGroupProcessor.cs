using System.Net.Http.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Cirrus.ExtensionRuntime;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Processors
{
    public class GetResourceGroupProcessor : ResourcesProcessorBase
    {
        [ArgumentInfo(
        Required = false,
        Description = @"Azure Subscription Id.",
        SerializedName = @"SubscriptionId")]
        public string? SubscriptionId { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"The name of the resource group.",
        SerializedName = @"Name")]
        public string? Name { get; set; }

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            // Use provided subscription ID or fall back to auth context
            var subscriptionId = SubscriptionId ?? AuthContext.SubscriptionId;
            // construct URL
            var url = $"https://management.azure.com/subscriptions/{subscriptionId}/resourcegroups/{Name}?api-version=2025-04-01";
            //TODO(Yabo): setJsonOptions from Processor context
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
            await Publisher.Publish(Events.TracingHttpResponseMessage, response);
            await response.EnsureSuccessAsync(Publisher);
            ResourceGroup rg = (await response.Content.ReadFromJsonAsync<ResourceGroup>(JsonOptions))!;
            await WriteOutput(rg);
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
