// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Agent.Tools.Help;
using Core.AssemblyLoading;
using Core.Cli;
using Core.Configuration.Abstractions;
using Core.Configuration.Implementation;
using Core.Configuration.Schema;
using Core.Extensions;
using Core.Runtime;
using Microsoft.Azure.Azclips.Extensions.Agent.Processors;
using Tests.Common.Stubs;
using Tests.Common.Utilities;
using CliHelp = Core.Cli.Help;

namespace Core.Agent.Tests;

internal sealed class HelpCatalogFixture : IDisposable
{
    public string Root { get; } = Path.Combine(RepoLocator.GetRepoRoot(), "artifacts", "direct-read-tests", Guid.NewGuid().ToString("N"));
    public string CliDirectory { get; }
    public string Executable { get; }
    public string InstalledRoot { get; private set; }
    public string CacheDirectory { get; }
    public IConfigurationService Configuration { get; }
    public HelpCatalogProvider Provider { get; }
    public int GenerationCount;

    public HelpCatalogFixture()
    {
        CliDirectory = Path.Combine(Root, "layout", "Core.Cli");
        InstalledRoot = Path.Combine(Root, "installed");
        CacheDirectory = Path.Combine(Root, "home", HomePaths.AgentDirectoryName, HomePaths.AgentHelpDirectoryName);
        Directory.CreateDirectory(CliDirectory);
        Directory.CreateDirectory(InstalledRoot);
        File.Copy(typeof(CliHelp.HelpArtifactWriter).Assembly.Location, Path.Combine(CliDirectory, "az.dll"));
        Executable = Path.Combine(CliDirectory, OperatingSystem.IsWindows() ? "az.exe" : "az");

        var schema = new ConfigurationSchema();
        Configuration = new ConfigurationService(
            new FixtureConfigurationProvider(Path.Combine(Root, "config.json")), schema, new DefaultConfigurationValidator(schema));
        WriteConfiguration();
        Provider = new HelpCatalogProvider(Executable, Configuration, CacheDirectory, RegenerateAsync);
    }

    public void ChangeInstalledRoot(string root)
    {
        InstalledRoot = root;
        Directory.CreateDirectory(root);
        WriteConfiguration();
    }

    private void WriteConfiguration() => File.WriteAllText(Configuration.GetConfigFilePath(), JsonSerializer.Serialize(new
    {
        assemblyLoading = new { installedExtensionsRoot = InstalledRoot, bypassExtensionCache = true },
    }));

    public IAssemblyLoadingService CreateLoader()
    {
        var logging = new NullLoggingService();
        return new AssemblyLoadingService(new CLIAssemblyLoadingProvider(
            [new BundledExtensionLoader(logging, Configuration, CliDirectory), new InstalledExtensionLoader(logging, Configuration)],
            logging), new DefaultAssemblyVersionProvider());
    }

    public Task<string> RegenerateAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref GenerationCount);
        return Task.FromResult(HelpGenerateProcessor.Generate(CreateLoader(), Configuration, Executable, CacheDirectory, ct));
    }

    public string Install(string name, string version, Assembly assembly, bool bundled = false)
    {
        var package = ExtensionDiscoveryPaths.PackagePrefix + name;
        var directory = bundled
            ? Path.Combine(ExtensionDiscoveryPaths.GetDefaultBundledRoot(CliDirectory), package)
            : Path.Combine(InstalledRoot, package, version);
        Directory.CreateDirectory(directory);
        File.Copy(assembly.Location, Path.Combine(directory, package + ".dll"), overwrite: true);
        if (bundled)
        {
            using var archive = ZipFile.Open(Path.Combine(directory, $"{package}.{version}.nupkg"), ZipArchiveMode.Create);
            using var writer = new StreamWriter(archive.CreateEntry(package + ".nuspec").Open());
            writer.Write($"<package><metadata><id>{package}</id><version>{version}</version><authors>Tests</authors><description>Offline fixture</description></metadata></package>");
        }
        return directory;
    }

    public IList<CliHelp.HelpManifest> BaselineManifests() =>
        (CreateLoader().LoadAllExtensions() ?? [])
            .SelectMany(x => CliHelp.ExtensionHelpManifestBuilder.BuildManifests(x.Load().GetExtensionIndex(), x.Info.Version)).ToList();

    public void WriteCatalog(string? directory = null, IList<CliHelp.HelpManifest>? manifests = null)
    {
        directory ??= CacheDirectory;
        Directory.CreateDirectory(directory);
        var stamp = HelpCatalogFingerprint.Read(Executable, Configuration);
        manifests ??= BaselineManifests();
        CliHelp.HelpArtifactWriter.WriteCatalog(directory,
            new CliHelp.HelpCatalogHeader(stamp.SchemaVersion, stamp.CoreVersion, stamp.ExtensionFingerprint),
            manifests, CliHelp.ExtensionHelpManifestBuilder.BuildIndex(manifests));
    }

    public void Dispose()
    {
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
    }

    private sealed class FixtureConfigurationProvider(string path) : Core.Configuration.Abstractions.IConfigurationProvider
    {
        public string GetConfigFilePath() => path;
        public async Task<JsonObject> ReadAsync(CancellationToken cancellationToken = default) =>
            File.Exists(path) ? JsonNode.Parse(await File.ReadAllTextAsync(path, cancellationToken))!.AsObject() : [];
        public Task WriteAsync(JsonObject configuration, CancellationToken cancellationToken = default) =>
            File.WriteAllTextAsync(path, configuration.ToJsonString(), cancellationToken);
    }
}
