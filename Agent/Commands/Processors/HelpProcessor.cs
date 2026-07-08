// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Agent.Tools.Help;
using Core.Configuration.Options;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors
{
    public class HelpProcessor : AgentProcessorBase
    {
        [ArgumentInfo(Required = false, Description = @"Command name to look up, e.g. 'vm create'. Omit to list all known commands.", SerializedName = @"name")]
        public string? Name { get; set; }

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);

            // Loader used LoadFromStream, so Assembly.Location is empty.
            // Reconstruct the extension's on-disk directory by asking the host
            // where it puts built-in extensions — Release lays them under
            // <base>/Extensions/, Debug puts them under <base>/.. — instead of
            // hard-coding "Extensions/" and breaking local debug runs.
            var configService = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();
            var loadingOpts = configService.GetConfiguration<AssemblyLoadingOptions>();
            var extName = typeof(HelpProcessor).Assembly.GetName().Name!;
            var corpusDir = Path.Combine(loadingOpts.BuiltinInstallationDir, extName, "Help");
            var help = new HelpTools(corpusDir);

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
