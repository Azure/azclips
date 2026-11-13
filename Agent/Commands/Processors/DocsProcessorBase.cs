// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Agent.Tools.Help;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors;

/// <summary>CLI and agent tools share one read/lookup contract; only cold generation reflects extensions.</summary>
public abstract class DocsProcessorBase : AgentProcessorBase
{
    protected abstract Task<string> ReadJsonAsync(HelpCatalogProvider provider);

    /// <summary>Provider over the default agent help cache that regenerates in process.</summary>
    internal static HelpCatalogProvider CreateHelpCatalogProvider()
    {
        var configuration = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();
        var azPath = Path.Combine(AppContext.BaseDirectory, "az.dll");
        var cache = HomePaths.GetDefaultAgentHelpDirectory();
        var provider = new HelpCatalogProvider(azPath, configuration, cache, ct => Task.FromResult(
            DocsGenerateProcessor.Generate(AzclipsServiceProvider.GetRequiredService<IAssemblyLoadingService>(),
                configuration, azPath, cache, ct)));
        return provider;
    }

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        var provider = CreateHelpCatalogProvider();
        await WriteOutput(await ReadJsonAsync(provider));
        await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
    }
}
