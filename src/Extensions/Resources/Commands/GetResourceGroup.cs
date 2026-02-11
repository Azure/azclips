using System.Net.Http.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Commands
{
    [CommandInfo(Name = "get", Description = "Get a resource group.")]
    public class GetResourceGroupCommand : ResourceCommandBase
    {
        public GetResourceGroupCommand(
            IRuntimeHttpPipeline httpPipeline,
            ICommandOutputHandler outputHandler,
            ICommandTelemetryHandler telemetryHandler)
            : base(httpPipeline, outputHandler, telemetryHandler) { }

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
            using var _ = StartActivity($"{nameof(GetResourceGroupCommand)}.{nameof(ProcessAsync)}");
            // construct URL
            var url = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/{Name}?api-version=2025-04-01";
            //TODO: set_jsonOptions from command context
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            HttpResponseMessage response = await HttpPipeline.SendAsync(request, this);
            ResourceGroup rg = (await response.Content.ReadFromJsonAsync<ResourceGroup>(_jsonOptions))!;
            await WriteOutput(rg);
        }
    }
}
