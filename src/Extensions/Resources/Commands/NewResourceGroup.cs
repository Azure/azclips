using System.Net.Http.Headers;
using System.Net.Http.Json;
using Core.Abstractions;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Commands
{
    [CommandInfo(Name = "create", Description = "Create a resource group.")]
    public class NewResourceGroupCommand : ResourceCommandBase
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
            using var _ = StartActivity($"{nameof(NewResourceGroupCommand)}.{nameof(ProcessAsync)}");
            // construct URL
            var url = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/{Name}?api-version=2025-04-01";
            //TODO: set_jsonOptions from command context
            using var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = JsonContent.Create(new ResourceGroup("eastus", Name), options: _jsonOptions)
            };
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
            HttpResponseMessage response = await HttpPipeline.SendAsync(request, this);
            ResourceGroup rg = (await response.Content.ReadFromJsonAsync<ResourceGroup>(_jsonOptions))!;
            await WriteOutput(rg);
        }
    }
}