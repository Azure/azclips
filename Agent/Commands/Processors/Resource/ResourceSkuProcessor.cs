// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Agent.Tools.Resource.Sku;
using Microsoft.Extensions.AI;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors.Resource
{
    public class ResourceSkuProcessor : AgentProcessorBase
    {
        [ArgumentInfo(Required = true, Description = @"VM SKU to check, e.g. 'Standard_D2s_v5'.", SerializedName = @"vm-size")]
        public string VmSize { get; set; } = string.Empty;

        [ArgumentInfo(Required = false, Description = @"Optional subscription id. Defaults to the current auth-context subscription.", SerializedName = @"subscription-id")]
        public string? SubscriptionId { get; set; }

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            await RunToolCommandAsync(ExecuteToolAsync);
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }

        private async Task ExecuteToolAsync(Func<string, IDisposable?> activityCreator)
        {
            var authService = GetService<IAuthService>();
            var profileService = GetService<IProfileService>();
            // Reuse the DI-registered process-wide HttpClient instead of letting
            // AvailableLocationsTool construct (and leak) its own on every
            // `az agent tool resource sku` invocation. Matches ResourceProcessorSetup.
            var httpClient = GetService<HttpClient>();
            var tool = new AvailableLocationsTool(authService, profileService, httpClient, activityCreator);

            var args = new AIFunctionArguments
            {
                ["vmSize"] = VmSize,
            };
            if (!string.IsNullOrWhiteSpace(SubscriptionId))
            {
                args["subscriptionId"] = SubscriptionId;
            }

            var result = await tool.FindAvailableLocations.InvokeAsync(args, Token);
            await WriteOutput(result?.ToString() ?? "{}");

        }
    }
}
