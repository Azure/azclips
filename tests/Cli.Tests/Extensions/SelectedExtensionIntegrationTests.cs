// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.AssemblyLoading;
using Core.Cli;
using Core.Cli.Completions;
using Core.Cli.Exceptions;
using Core.Cli.Help;
using Core.Cli.Parser;
using Core.Configuration.Options;
using Core.DependencyInjection;
using Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Tests.Common.Stubs;

namespace Cli.Tests;

[Collection("Azclips Service Provider Tests")]
public sealed class SelectedExtensionIntegrationTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.0", false, "alpha", false)]
    [InlineData("1.0.0", "1.0.0", false, "alpha", true)]
    [InlineData("1.0.0", "2.0.0", false, "beta", false)]
    [InlineData("1.0.0", "2.0.0", false, "beta", true)]
    [InlineData("2.0.0", "1.0.0", true, "beta", false)]
    [InlineData("2.0.0", "1.0.0", true, "beta", true)]
    public async Task RoutingExecutionHelpAndCompletionsUseTheSameCopy(
        string bundledVersion, string installedVersion, bool pinOlder, string expectedCommand, bool completionsFirst)
    {
        using var fixture = new Fixture();
        var bundledDll = fixture.Stage("SharedAlpha", bundledVersion, bundled: true);
        var installedDll = fixture.Stage("SharedBeta", installedVersion, bundled: false);
        if (pinOlder)
        {
            fixture.Stage("SharedAlpha", "3.0.0", bundled: false);
            fixture.Pin(installedVersion);
        }

        fixture.Initialize();
        var dispatcher = AzclipsServiceProvider.GetRequiredService<ExtensionDispatcher>();
        var rejectedCommand = expectedCommand == "alpha" ? "beta" : "alpha";
        var expectedVersion = expectedCommand == "alpha" ? bundledVersion : installedVersion;
        var expectedDll = expectedCommand == "alpha" ? bundledDll : installedDll;

        // Help and completions must initialize routing themselves, not depend on prior execution.
        if (!completionsFirst)
        {
            Assert.Equal(0, fixture.Help.RenderExtensionHelp(["sharedverb"]));
        }
        var candidates = await CaptureConsoleAsync(() =>
            CompletionGenerator.TryHandleCandidatesAsync(["--completion-candidates", "sharedverb"], ClientType.CLI));
        if (completionsFirst)
        {
            Assert.Equal(0, fixture.Help.RenderExtensionHelp(["sharedverb"]));
        }
        Assert.Contains($"{expectedCommand}:", fixture.HelpOutput.ToString());
        Assert.DoesNotContain($"{rejectedCommand}:", fixture.HelpOutput.ToString());
        Assert.Equal(expectedCommand, candidates.Trim());
        var tree = await CaptureConsoleAsync(() =>
            CompletionGenerator.TryHandleAsync(["--completions"], ClientType.CLI));
        using (var json = JsonDocument.Parse(tree))
        {
            var group = json.RootElement.GetProperty("sharedverb");
            Assert.True(group.TryGetProperty(expectedCommand, out _));
            Assert.False(group.TryGetProperty(rejectedCommand, out _));
        }

        var invocation = dispatcher.Parse(["sharedverb", expectedCommand]);
        Assert.Equal(expectedVersion, invocation.ModuleVersion);
        Assert.Throws<CommandNotFoundException>(() => dispatcher.Parse(["sharedverb", rejectedCommand]));
        fixture.Logger.Verify(log => log.Debug(It.Is<string>(text =>
            text.Contains($"sidecars={(pinOlder ? 3 : 2)}, DLL fallbacks=0, failures=0"))), Times.Once);
        using (var session = fixture.Engine.OpenExecutionSession(invocation, fixture.Streams))
        {
            session.Run();
        }
        Assert.Equal([$"{expectedCommand}-payload"], fixture.Outputs);
        Assert.All(fixture.Engine.DiscoveryRequests,
            names => Assert.Equal([fixture.PackageId], names));
        Assert.Equal(3, fixture.Engine.DiscoveryRequests.Count);
        fixture.Logger.Verify(log => log.Debug(It.Is<string>(text =>
            text.Contains("Loaded '") && text.Contains($"from '{expectedDll}'"))), Times.AtLeastOnce);
        fixture.Logger.Verify(log => log.Debug(It.Is<string>(text =>
            text.Contains("Loaded '") && !text.Contains($"from '{expectedDll}'"))), Times.Never);
    }

    [Fact]
    public async Task EmptyRoutingSelectionNeverOpensAllCopiesDiscovery()
    {
        using var fixture = new Fixture();
        fixture.Initialize();

        Assert.Equal(0, fixture.Help.RenderRoot());
        Assert.DoesNotContain("sharedverb", fixture.HelpOutput.ToString());
        var candidates = await CaptureConsoleAsync(() =>
            CompletionGenerator.TryHandleCandidatesAsync(["--completion-candidates"], ClientType.CLI));
        Assert.Equal(string.Empty, candidates.Trim());
        var tree = await CaptureConsoleAsync(() =>
            CompletionGenerator.TryHandleAsync(["--completions"], ClientType.CLI));
        Assert.Equal("{}", tree.Trim());
        Assert.Empty(fixture.Engine.DiscoveryRequests);
        fixture.Logger.Verify(log => log.Debug(It.Is<string>(text => text.Contains("Loaded '"))), Times.Never);
    }

    private static async Task<string> CaptureConsoleAsync(Func<Task<int?>> action)
    {
        var original = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            Assert.Equal(0, await action());
            return output.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"selected-integration-{Guid.NewGuid():N}");
        private readonly string _bundledDirectory;
        private readonly FileExtensionRegistryStore _registry;

        public Fixture()
        {
            PackageId = $"Microsoft.Azure.Azclips.Extensions.Selection_{Guid.NewGuid():N}";
            _bundledDirectory = Path.Combine(BundledExtensionLoader.GetDefaultRoot(), PackageId);
            Directory.CreateDirectory(_root);
            _registry = new FileExtensionRegistryStore(Path.Combine(_root, "registry.json"));
            Streams = new OutputStreams(
                WriteOutput: (value, _) => { Outputs.Add(value); return Task.CompletedTask; },
                WriteError: (value, _) => throw new InvalidOperationException($"Unexpected error: {value}"),
                WriteWarning: (value, _) => throw new InvalidOperationException($"Unexpected warning: {value}"),
                WriteVerbose: (_, _) => Task.CompletedTask,
                WriteDebug: (_, _) => Task.CompletedTask,
                WriteInformation: (_, _) => Task.CompletedTask);
        }

        public string PackageId { get; }
        public Mock<ILoggingService> Logger { get; } = new();
        public List<object> Outputs { get; } = [];
        public OutputStreams Streams { get; }
        public StringWriter HelpOutput { get; } = new();
        public RuntimeHelpDispatcher Help { get; private set; } = null!;
        public RecordingEngine Engine => AzclipsServiceProvider.GetRequiredService<RecordingEngine>();

        public string Stage(string fixtureName, string version, bool bundled)
        {
            var directory = bundled ? _bundledDirectory : Path.Combine(_root, "installed", PackageId, version);
            Directory.CreateDirectory(directory);
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            var repo = new DirectoryInfo(AppContext.BaseDirectory);
            while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "azclips.slnx")))
            {
                repo = repo.Parent;
            }
            Assert.NotNull(repo);
            var source = Path.Combine(repo.FullName, "tests", "Fixtures", fixtureName, "bin",
                configuration, "net10.0", $"Azclips.Tests.Fixtures.{fixtureName}.dll");
            var dll = Path.Combine(directory, $"{PackageId}.dll");
            File.Copy(source, dll);
            if (bundled)
            {
                File.WriteAllText(Path.Combine(directory, $"{PackageId}.{version}.nupkg"), string.Empty);
            }
            var command = fixtureName == "SharedAlpha" ? "alpha" : "beta";
            File.WriteAllText(Path.ChangeExtension(dll, ".commands.json"), JsonSerializer.Serialize(new
            {
                schemaVersion = 1, packageId = PackageId, version,
                verbs = new[] { "sharedverb" }, leaves = new[] { $"sharedverb {command}" },
            }));
            return dll;
        }

        public void Pin(string version)
        {
            var registry = new ExtensionRegistry();
            registry.Extensions[PackageId] = new ExtensionRegistryEntry { Version = version, Pinned = true };
            _registry.Save(registry);
        }

        public void Initialize()
        {
            AzclipsServiceProvider.Dispose();
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton<IConfigurationService>(new Configuration(_root));
                services.AddSingleton(Logger.Object);
                services.AddSingleton<ITelemetryService, StubTelemetryService>();
                services.AddSingleton<IProfileService, StubProfileService>();
                services.AddSingleton<IAuthService, StubAuthService>();
                services.AddSingleton<PropertyParser>();
                services.AddSingleton<IArgumentParsingProvider, CLIArgumentParsingProvider>();
                services.AddCliExtensionDispatch();
                services.AddSingleton<IExtensionRegistryStore>(_registry);
                // Filter only enumeration, to keep unrelated build/user extensions out of this fixture.
                // Disk enumeration, path loading, and tier ordering still use the real loaders.
                services.AddSingleton<IExtensionLoader>(sp => new IsolatedLoader(
                    new InstalledExtensionLoader(Logger.Object, sp.GetRequiredService<IConfigurationService>()), PackageId));
                services.AddSingleton<IExtensionLoader>(sp => new IsolatedLoader(
                    new BundledExtensionLoader(Logger.Object, sp.GetRequiredService<IConfigurationService>()), PackageId));
                services.AddSingleton<IAssemblyLoadingProvider, CLIAssemblyLoadingProvider>();
                services.AddSingleton<IAssemblyLoadingService, AssemblyLoadingService>();
                services.AddSingleton(sp => new RecordingEngine(ActivatorUtilities.CreateInstance<Core.Engine.Engine>(sp)));
                services.AddSingleton<IEngine>(sp => sp.GetRequiredService<RecordingEngine>());
            });
            AzclipsServiceProvider.Initialize(new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));
            Help = new RuntimeHelpDispatcher(Engine, Streams, HelpOutput)
            {
                SelectedPackageIds = () => CliExtensionServiceRegistration.GetRoutingSelectedPackageIds(
                    AzclipsServiceProvider.GetRequiredService<ExtensionDispatcher>(),
                    AzclipsServiceProvider.GetRequiredService<SelectedExtensionLocations>()),
            };
        }

        public void Dispose()
        {
            AzclipsServiceProvider.Dispose();
            HelpOutput.Dispose();
            if (Directory.Exists(_bundledDirectory))
            {
                Directory.Delete(_bundledDirectory, recursive: true);
            }
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class IsolatedLoader(IExtensionLoader inner, string packageId) : IExtensionLoader
    {
        public ExtensionTier Tier => inner.Tier;
        public IEnumerable<(string Name, string Version, string? Location)> GetInstalledExtensions() =>
            inner.GetInstalledExtensions().Where(item => item.Location is not null &&
                Path.GetFileName(item.Location) == $"{packageId}.dll");
        public Assembly? LoadExtension(string extensionName, string? version = null) =>
            throw new InvalidOperationException("Selected-copy consumers must not search by name/version.");
        public Assembly? LoadExtensionFromPath(string extensionName, string? version, string assemblyPath) =>
            inner.LoadExtensionFromPath(extensionName, version, assemblyPath);
    }

    private sealed class RecordingEngine(IEngine inner) : IEngine
    {
        public List<string[]> DiscoveryRequests { get; } = [];
        public ISession OpenExecutionSession(CommandInvocation invocation, OutputStreams outputStreams,
            CancellationToken cancellationToken = default) =>
            inner.OpenExecutionSession(invocation, outputStreams, cancellationToken);
        public ISession OpenDiscoverySession(IList<string> extensionNames, string? commandName,
            OutputStreams outputStreams, bool isTransient = false, CancellationToken cancellationToken = default)
        {
            DiscoveryRequests.Add(extensionNames.ToArray());
            return inner.OpenDiscoverySession(extensionNames, commandName, outputStreams, isTransient, cancellationToken);
        }
        public void Dispose() => inner.Dispose();
    }

    private sealed class Configuration(string root) : IConfigurationService
    {
        public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new()
        {
            var result = defaultValue ?? new T();
            if (result is AssemblyLoadingOptions options)
            {
                options.InstalledExtensionsRoot = Path.Combine(root, "installed");
            }
            return result;
        }
        public Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult<object?>(null);
        public string GetConfigFilePath() => Path.Combine(root, "config.json");
        public IConfigurationSchema GetSchema() => throw new NotSupportedException();
    }
}
