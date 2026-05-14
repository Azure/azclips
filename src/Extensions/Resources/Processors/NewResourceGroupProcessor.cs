using System.Net.Http.Headers;
using System.Net.Http.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Processors
{
    public class NewResourceGroupProcessor : ResourcesProcessorBase
    {
        [ArgumentInfoAttribute(
        Required = true,
        Description = @"Azure Subscription Id.",
        SerializedName = @"SubscriptionId")]
        public string? SubscriptionId { get; set; }

        [ArgumentInfo(
        Required = true,
        Description = @"The name of the resource group.",
        SerializedName = @"Name")]
        public string Name { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"The resource id of the resource group.",
        SerializedName = @"ResourceId")]
        public string ResourceId { get; set; } = "";

        [ArgumentInfo(
        Required = true,
        Description = @"Location for resource group.",
        SerializedName = @"Location")]
        public string Location { get; set; } = "";

        public override async Task ProcessAsync()
        {
            await EventListener.Signal(Events.ProcessorProcessAsyncStart);
            // Use provided subscription ID or fall back to auth context
            var subscriptionId = SubscriptionId ?? AuthContext.SubscriptionId;
            // construct URL
            var url = $"https://management.azure.com/subscriptions/{subscriptionId}/resourcegroups/{Name}?api-version=2025-04-01";
            //TODO: setJsonOptions from Processor context
            using var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = JsonContent.Create(new ResourceGroup(Location, Name), options: JsonOptions)
            };
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
            using HttpResponseMessage response = await HttpPipeline.SendAsync(request, EventListener);
            await EventListener.Signal(Events.TracingHttpResponseMessage, response);
            ResourceGroup rg = (await response.Content.ReadFromJsonAsync<ResourceGroup>(JsonOptions))!;
            await WriteOutput(rg);
            await EventListener.Signal(Events.ProcessorProcessAsyncEnd);
        }
    }
}