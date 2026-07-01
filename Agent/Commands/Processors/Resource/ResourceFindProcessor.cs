// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Agent.Tools.Resource.Cache;
using Core.DependencyInjection;
using Microsoft.Extensions.AI;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors.Resource
{
    public class ResourceFindProcessor : AgentProcessorBase
    {
        [ArgumentInfo(Required = true, Description = @"Substring to match against resource names.", SerializedName = @"query")]
        public string Query { get; set; } = string.Empty;

        [ArgumentInfo(Required = false, Description = @"Resource kind filter (e.g. 'resources', 'resource-groups'). Defaults to all kinds.", SerializedName = @"kind")]
        public string? Kind { get; set; }

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);

            var authService = CirrusServiceProvider.GetRequiredService<IAuthService>();
            var armDetails = new ArmDetailsClient(authService);
            var tool = new CachedResourceTools(armDetails);

            var args = new AIFunctionArguments
            {
                ["query"] = Query,
            };
            if (!string.IsNullOrWhiteSpace(Kind))
            {
                args["kind"] = Kind;
            }

            var result = await tool.FindResources.InvokeAsync(args, Token);
            await WriteOutput(result?.ToString() ?? "{}");

            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
