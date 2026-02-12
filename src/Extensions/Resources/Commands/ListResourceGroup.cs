using System.Net.Http.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Commands
{
    [CommandInfo(Name = "list", Description = "List resource groups.")]
    public class ListResourceGroupCommand : ResourceCommandBase
    {
        public ListResourceGroupCommand(
            IRuntimeHttpPipeline httpPipeline,
            ICommandOutputHandler outputHandler,
            ICommandTelemetryHandler telemetryHandler,
            ICommandAuthHandler authHandler)
            : base(httpPipeline, outputHandler, telemetryHandler, authHandler) { }

        [ArgumentInfo(
        Required = false,
        Description = @"Azure Subscription Id.",
        SerializedName = @"SubscriptionId")]
        public string? SubscriptionId { get; set; }

        public override async Task ProcessAsync()
        {
            using var _ = StartActivity($"{nameof(ListResourceGroupCommand)}.{nameof(ProcessAsync)}");
            // Use provided subscription ID or fall back to auth context
            var subscriptionId = SubscriptionId ?? AuthContext.SubscriptionId;
            // construct URL
            var url = $"https://management.azure.com/subscriptions/{subscriptionId}/resourcegroups?api-version=2025-04-01";
            //TODO: set_jsonOptions from command context
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            HttpResponseMessage response = await HttpPipeline.SendAsync(request, this);
            ResourceGroupListResult rg = (await response.Content.ReadFromJsonAsync<ResourceGroupListResult>(_jsonOptions))!;
            await WriteOutput(rg.Value);
        }
    }
}