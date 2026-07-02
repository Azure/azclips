// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Agent.Tools.Resource.Sku;
using Core.DependencyInjection;
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

            var authService = AzclipsServiceProvider.GetRequiredService<IAuthService>();
            var authContext = AzclipsServiceProvider.GetRequiredService<IAuthContextService>();
            var tool = new AvailableLocationsTool(authService, authContext);

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

            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
