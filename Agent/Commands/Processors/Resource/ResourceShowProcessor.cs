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

        [ArgumentInfo(Required = false, Description = @"Set true to also fetch live ARM body (properties, provisioningState, endpoints). Adds ~200-500ms. Default false.", SerializedName = @"live")]
        public bool Live { get; set; }

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);

            var (tools, _) = await ResourceProcessorSetup.InitializeAsync(Token);
            var args = new AIFunctionArguments
            {
                ["nameOrId"] = Name,
                ["live"] = Live,
            };
            if (!string.IsNullOrWhiteSpace(Kind))
            {
                args["kind"] = Kind;
            }

            var result = await tools.GetResourceDetails.InvokeAsync(args, Token);
            await WriteOutput(result?.ToString() ?? "{}");

            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
