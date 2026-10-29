// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Agent.Tools.Env;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors
{
    public class EnvProcessor : AgentProcessorBase
    {
        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);

            var profileService = AzclipsServiceProvider.GetRequiredService<IProfileService>();
            await WriteOutput(await EnvSnapshotProvider.GetJsonAsync(
                profileService, Path.Combine(AppContext.BaseDirectory, "az.dll"), Token));
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
