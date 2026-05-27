using System.Net.Http.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Telemetry;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Processors
{
    public class ListResourceGroupProcessor : ResourcesProcessorBase
    {
        [ArgumentInfo(
        Required = false,
        Description = @"Azure Subscription Id.",
        SerializedName = @"SubscriptionId")]
        public string? SubscriptionId { get; set; }

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            // Use provided subscription ID or fall back to auth context
            var subscriptionId = SubscriptionId ?? AuthContext.SubscriptionId;
            // construct URL
            var url = $"https://management.azure.com/subscriptions/{subscriptionId}/resourcegroups?api-version=2025-04-01";
            //TODO(Yabo): setJsonOptions from Processor context
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
            await Publisher.Publish(Events.TracingHttpResponseMessage, response);
            ResourceGroupListResult rg = (await response.Content.ReadFromJsonAsync<ResourceGroupListResult>(JsonOptions))!;
            await WriteOutput(rg.Value);
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
