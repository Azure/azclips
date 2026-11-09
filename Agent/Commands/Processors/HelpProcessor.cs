// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Agent.Tools.Help;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors;

/// <summary>CLI and agent tools share one read/lookup contract; only cold generation reflects extensions.</summary>
public abstract class HelpProcessor : AgentProcessorBase
{
    protected abstract Task<string> ReadJsonAsync(HelpCatalogProvider provider);

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        var configuration = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();
        var azPath = Path.Combine(AppContext.BaseDirectory, "az.dll");
        var cache = HomePaths.GetDefaultAgentHelpDirectory();
        var provider = new HelpCatalogProvider(azPath, configuration, cache, ct => Task.FromResult(
            HelpGenerateProcessor.Generate(AzclipsServiceProvider.GetRequiredService<IAssemblyLoadingService>(),
                configuration, azPath, cache, ct)));
        await WriteOutput(await ReadJsonAsync(provider));
        await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
    }
}
