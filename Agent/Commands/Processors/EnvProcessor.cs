// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Agent.Tools.Env;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors
{
    public class EnvProcessor : AgentProcessorBase
    {
        private static readonly JsonSerializerOptions s_json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);

            var authContext = AzclipsServiceProvider.GetRequiredService<IAuthContextService>();
            var prober = new EnvironmentProber(authContext, flavor: "cli");
            var snapshot = await prober.ProbeAsync(Token);

            await WriteOutput(JsonSerializer.Serialize(snapshot, s_json));
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
