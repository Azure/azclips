// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Agent.Tools.Help;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors
{
    public class HelpProcessor : AgentProcessorBase
    {
        [ArgumentInfo(Required = false, Description = @"Command name to look up, e.g. 'vm create'. Omit to list all known commands.", SerializedName = @"name")]
        public string? Name { get; set; }

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);

            var help = new HelpTools();
            string json;
            if (string.IsNullOrWhiteSpace(Name))
            {
                var listResult = await help.ListCommands.InvokeAsync();
                json = listResult?.ToString() ?? "{}";
            }
            else
            {
                json = help.LookupCommand(Name);
            }

            await WriteOutput(json);
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
