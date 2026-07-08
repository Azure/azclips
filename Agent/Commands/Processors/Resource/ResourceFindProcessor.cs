// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Extensions.AI;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors.Resource
{
    public class ResourceFindProcessor : AgentProcessorBase
    {
        [ArgumentInfo(Required = true, Description = @"Name pattern to match. Exact match preferred, falls back to substring, then Levenshtein typo correction.", SerializedName = @"name")]
        public string Name { get; set; } = string.Empty;

        [ArgumentInfo(Required = false, Description = @"Optional kind filter: tenant | subscription | resourceGroup | resource. When omitted, all kinds are searched.", SerializedName = @"kind")]
        public string? Kind { get; set; }

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);

            var (tools, _) = ResourceProcessorSetup.Initialize();
            var args = new AIFunctionArguments
            {
                ["namePattern"] = Name,
            };
            if (!string.IsNullOrWhiteSpace(Kind))
            {
                args["kind"] = Kind;
            }

            var result = await tools.FindResources.InvokeAsync(args, Token);
            await WriteOutput(result?.ToString() ?? "{}");

            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
