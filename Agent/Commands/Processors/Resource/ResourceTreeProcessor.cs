// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Extensions.AI;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors.Resource
{
    /// <summary>
    /// `az agent tool resource tree` — mirrors the agent-side get_resource_tree
    /// LLM tool. Shares the same CachedResourceTools business layer, so the
    /// JSON shape is byte-identical (external skill consumers / tests can
    /// invoke the command and get exactly what the LLM would).
    /// </summary>
    public class ResourceTreeProcessor : AgentProcessorBase
    {
        [ArgumentInfo(Required = true, Description = @"Name, full ARM id, subscription id/name, or tenant id. Use kind to disambiguate.", SerializedName = @"name")]
        public string Name { get; set; } = string.Empty;

        [ArgumentInfo(Required = true, Description = @"Kind of node to root the tree at: tenant | subscription | resourceGroup | resource.", SerializedName = @"kind")]
        public string Kind { get; set; } = string.Empty;

        [ArgumentInfo(Required = false, Description = @"Max depth from the root node. Defaults to 2 (sub→RG, RG→resource).", SerializedName = @"max-depth")]
        public int MaxDepth { get; set; } = 2;

        [ArgumentInfo(Required = false, Description = @"Optional subscription scope when kind=resourceGroup and the RG name repeats across subscriptions.", SerializedName = @"subscription-id")]
        public string? Subscription { get; set; }

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
                ["kind"] = Kind,
                ["maxDepth"] = MaxDepth,
            };
            if (!string.IsNullOrWhiteSpace(Subscription))
            {
                args["subscription"] = Subscription;
            }

            var result = await tools.GetResourceTree.InvokeAsync(args, Token);
            await WriteOutput(result?.ToString() ?? "{}");

        }
    }
}
