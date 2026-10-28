// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Agent.Tools.Help;
using Core.Cli.Help;
using Core.Configuration.Options;
using Core.DependencyInjection;
using Core.Extensions;
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
        // The loader reports every copy. Select one per package with the CLI's selector and
        // pins, keyed like the sidecar reader and the registry (the DLL file name).
        var candidates = (loader.LoadAllExtensions() ?? [])
            .Select(static e => (Candidate: new DiscoveredExtensionInfo(
                string.IsNullOrWhiteSpace(e.Info.Location) ? e.Info.Name : Path.GetFileNameWithoutExtension(e.Info.Location),
                e.Info.Version, [], new Dictionary<string, CommandMetadata>(), e.Info.Location), e.Load))
            .ToList();
        var registry = ReadRegistry(configuration);
        foreach (var group in candidates.GroupBy(static c => c.Candidate.PackageId, StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            registry.Extensions.TryGetValue(group.Key, out var pin);
            var selection = ExtensionCandidateSelector.Select(group.Select(static c => c.Candidate).ToList(), pin);
            ExtensionCandidateSelector.ReportUnresolvablePin(group.Key, selection, pin, loggingService: null);
            var selected = group.First(c => ReferenceEquals(c.Candidate, selection.Selected));
            // A failed load must not be stamped as a complete, fresh catalog.
            manifests.AddRange(ExtensionHelpManifestBuilder.BuildManifests(
                selected.Load().GetExtensionIndex(), selected.Candidate.ResolvedVersion));
        }

        var after = HelpCatalogFingerprint.Read(azExePath, configuration, ct);
        if (before != after)
            throw new IOException("Extensions changed while generating help. Retry the command.");

        var header = new CliHelp.HelpCatalogHeader(after.SchemaVersion, after.CoreVersion, after.ExtensionFingerprint);
        HelpArtifactWriter.WriteCatalog(outputDirectory, header, manifests, ExtensionHelpManifestBuilder.BuildIndex(manifests), ct);
        return JsonSerializer.Serialize(new { generated = true, commands = manifests.Sum(m => m.Commands.Count) });
    }

    private static ExtensionRegistry ReadRegistry(IConfigurationService configuration)
    {
        var root = configuration.GetConfiguration<AssemblyLoadingOptions>().InstalledExtensionsRoot;
        return string.IsNullOrWhiteSpace(root)
            ? new ExtensionRegistry()
            : new FileExtensionRegistryStore(Path.Combine(Path.GetFullPath(root), HomePaths.RegistryFileName)).Load();
    }
}
