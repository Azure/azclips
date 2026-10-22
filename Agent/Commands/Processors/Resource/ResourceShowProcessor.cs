// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Extensions.AI;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors.Resource
{
    /// <summary>
    /// `az agent tool resource show` — mirrors the agent-side
    /// get_resource_details LLM tool. Cache-only by default; `--live` flips
    /// on the extra ARM GET for properties / provisioningState / etc.
    /// </summary>
    public class ResourceShowProcessor : AgentProcessorBase
    {
        [ArgumentInfo(Required = true, Description = @"Name or full ARM id. Use kind to disambiguate.", SerializedName = @"name")]
        public string Name { get; set; } = string.Empty;

        [ArgumentInfo(Required = false, Description = @"Optional kind filter: tenant | subscription | resourceGroup | resource. When omitted, all kinds are searched.", SerializedName = @"kind")]
        public string? Kind { get; set; }

        [ArgumentInfo(Required = false, Description = @"Optional subscription id or display name to scope the lookup.", SerializedName = @"subscription-id")]
        public string? Subscription { get; set; }

        [ArgumentInfo(Required = false, Description = @"Optional resource group to scope the lookup (only meaningful for kind=resource).", SerializedName = @"resource-group")]
        public string? ResourceGroup { get; set; }

        [ArgumentInfo(Required = false, Description = @"Set true to also fetch live ARM body (properties, provisioningState, endpoints). Adds ~200-500ms. Default false.", SerializedName = @"live")]
        public bool Live { get; set; }

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            await RunToolCommandAsync(ExecuteToolAsync);
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }

        private async Task ExecuteToolAsync(Func<string, IDisposable?> activityCreator)
        {
            var (tools, _) = ResourceProcessorSetup.Initialize(
                GetService<IAuthService>(), GetService<IProfileService>(), GetService<HttpClient>(), activityCreator);
            var args = new AIFunctionArguments
            {
                ["nameOrId"] = Name,
                ["live"] = Live,
            };
            if (!string.IsNullOrWhiteSpace(Kind))
            {
                args["kind"] = Kind;
            }
            if (!string.IsNullOrWhiteSpace(Subscription))
            {
                args["subscription"] = Subscription;
            }
            if (!string.IsNullOrWhiteSpace(ResourceGroup))
            {
                args["resourceGroup"] = ResourceGroup;
            }

            var result = await tools.GetResourceDetails.InvokeAsync(args, Token);
            await WriteOutput(result?.ToString() ?? "{}");

        }
    }
}
