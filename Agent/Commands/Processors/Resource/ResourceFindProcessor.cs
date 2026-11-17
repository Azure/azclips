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

        [ArgumentInfo(Required = false, Description = @"Optional subscription id or display name to narrow the search.", SerializedName = @"subscription-id")]
        public string? Subscription { get; set; }

        [ArgumentInfo(Required = false, Description = @"Optional resource group name to narrow the search (only meaningful for kind=resource).", SerializedName = @"resource-group")]
        public string? ResourceGroup { get; set; }

        [ArgumentInfo(Required = false, Description = @"Max candidates to return. Default 10.", SerializedName = @"top")]
        public int Top { get; set; } = 10;

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
                ["namePattern"] = Name,
                ["top"] = Top,
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

            var result = await tools.FindResources.InvokeAsync(args, Token);
            await WriteOutput(result?.ToString() ?? "{}");

        }
    }
}
