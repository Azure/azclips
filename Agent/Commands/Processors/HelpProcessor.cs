// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Agent.Tools.Help;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors;

/// <summary>CLI and agent tools share one read/lookup contract; only cold generation reflects extensions.</summary>
public class HelpProcessor : AgentProcessorBase
{
    [ArgumentInfo(Required = false, Description = @"Command name to look up, e.g. 'vm create'. Omit to list all known commands.", SerializedName = @"name")]
    public string? Name { get; set; }

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        var configuration = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();
        var azPath = Path.Combine(AppContext.BaseDirectory, "az.dll");
        var cache = HomePaths.GetDefaultAgentCacheDirectory();
        var provider = new HelpCatalogProvider(azPath, configuration, cache, ct => Task.FromResult(
            HelpGenerateProcessor.Generate(AzclipsServiceProvider.GetRequiredService<IAssemblyLoadingService>(),
                configuration, azPath, cache, ct)));
        var json = string.IsNullOrWhiteSpace(Name)
            ? await provider.ListJsonAsync(Token)
            : await provider.GetJsonAsync(Name, Token);
        await WriteOutput(json);
        await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
    }
}
