// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Agent.Tools.Help;
using Core.Cli.Help;
using Core.DependencyInjection;
using Core.Runtime;
using CliHelp = Core.Cli.Help;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors;

/// <summary>Generates help using the same runtime discovery and manifests as CLI help.</summary>
public class HelpGenerateProcessor : AgentProcessorBase
{
    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        var json = Generate(
            AzclipsServiceProvider.GetRequiredService<IAssemblyLoadingService>(),
            AzclipsServiceProvider.GetRequiredService<IConfigurationService>(),
            Path.Combine(AppContext.BaseDirectory, "az.dll"),
            HomePaths.GetDefaultAgentHelpDirectory(), Token);
        await WriteOutput(json);
        await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
    }

    public static string Generate(
        IAssemblyLoadingService loader, IConfigurationService configuration,
        string azExePath, string outputDirectory, CancellationToken ct = default)
    {
        var before = HelpCatalogFingerprint.Read(azExePath, configuration, ct);
        var manifests = new List<CliHelp.HelpManifest>();
        // IAssemblyLoadingService defines null as no discovered extensions, not
        // discovery failure. Exceptions must propagate before anything is published.
        var extensions = loader.LoadAllExtensions();
        foreach (var (info, load) in extensions ?? [])
        {
            ct.ThrowIfCancellationRequested();
            // A failed load must not be stamped as a complete, fresh catalog.
            manifests.AddRange(ExtensionHelpManifestBuilder.BuildManifests(load().GetExtensionIndex(), info.Version));
        }

        var after = HelpCatalogFingerprint.Read(azExePath, configuration, ct);
        if (before != after)
            throw new IOException("Extensions changed while generating help. Retry the command.");

        var header = new CliHelp.HelpCatalogHeader(after.SchemaVersion, after.CoreVersion, after.ExtensionFingerprint);
        HelpArtifactWriter.WriteCatalog(outputDirectory, header, manifests, ExtensionHelpManifestBuilder.BuildIndex(manifests), ct);
        return JsonSerializer.Serialize(new { generated = true, commands = manifests.Sum(m => m.Commands.Count) });
    }
}
