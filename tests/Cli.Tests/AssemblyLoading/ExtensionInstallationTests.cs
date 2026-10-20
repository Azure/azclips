// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.AssemblyLoading;
using Core.Cli;
using Core.Configuration.Options;
using Core.Engine;
using Core.Extensions;
using Moq;
using Tests.Common.Utilities;

namespace Cli.Tests;

[Collection("Extension Load Tracker Tests")]
public sealed class ExtensionInstallationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"clips980-{Guid.NewGuid():N}");
    private readonly string _name = $"Clips980_{Guid.NewGuid():N}";
    private readonly Mock<ILoggingService> _logging = new();
    private string PackageId => $"Microsoft.Azure.Azclips.Extensions.{_name}";
    private string BundledDirectory => Path.Combine(BundledExtensionLoader.GetDefaultRoot(), PackageId);
    private static Assembly Compute => typeof(Microsoft.Azure.Azclips.Extensions.Compute.ExtensionIndex).Assembly;
    private static Assembly Network => typeof(Microsoft.Azure.Azclips.Extensions.Network.ExtensionIndex).Assembly;

    private Mock<IConfigurationService> Configuration(bool bypass = false)
    {
        var configuration = new Mock<IConfigurationService>();
        configuration.Setup(service => service.GetConfiguration<AssemblyLoadingOptions>()).Returns(
            new AssemblyLoadingOptions { InstalledExtensionsRoot = _root, BypassExtensionCache = bypass });
        configuration.Setup(service => service.GetConfiguration<CoreOptions>()).Returns(new CoreOptions());
        configuration.Setup(service => service.GetConfiguration<BenchmarkOptions>()).Returns(new BenchmarkOptions());
        return configuration;
    }

    private string Stage(string directory, Assembly assembly)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{PackageId}.dll");
        File.Copy(assembly.Location, path, overwrite: true);
        return path;
    }

    private InstalledExtensionRef Enumerate(bool bundled, bool bypass = false)
    {
        IExtensionLoader loader = bundled
            ? new BundledExtensionLoader(_logging.Object, Configuration(bypass).Object)
            : new InstalledExtensionLoader(_logging.Object, Configuration(bypass).Object);
        return Assert.Single(loader.GetInstallations(), info => info.Name == _name);
    }

    [Fact]
    public void DeferredLoadsUseTheirOwningInstallation_NotTheFirstLoader()
    {
        Stage(BundledDirectory, Compute);
        File.WriteAllText(Path.Combine(BundledDirectory, $"{PackageId}.1.0.0.nupkg"), string.Empty);
        var installedPath = Stage(Path.Combine(_root, PackageId, "1.0.0"), Network);
        var config = Configuration().Object;
        var provider = new CLIAssemblyLoadingProvider(
            [new BundledExtensionLoader(_logging.Object, config), new InstalledExtensionLoader(_logging.Object, config)],
            _logging.Object, new InMemoryRegistryStore(new ExtensionRegistry()));
        var service = new AssemblyLoadingService(provider);

        var entries = service.LoadAllExtensions()!.Where(entry => entry.Info.Name == _name).ToArray();
        Assert.Equal(2, entries.Length);
        Assert.Equal(Compute.GetName().Name, provider.LoadExtension(_name, "1.0.0").GetName().Name);
        var installed = Assert.Single(entries, entry => entry.Info.Location == installedPath);
        Assert.Equal(Network.GetName().Name, installed.Load().GetName().Name);
        Assert.Equal(Network.GetName().Name, service.LoadExtension(installed.Info.Installation!).GetName().Name);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnumerationReusesIdentityAndAssemblyWithoutLoadingDuringDiscovery(bool bundled)
    {
        var directory = bundled ? BundledDirectory : Path.Combine(_root, PackageId, "1.0.0");
        Stage(directory, Compute);
        IExtensionLoader loader = bundled
            ? new BundledExtensionLoader(_logging.Object, Configuration().Object)
            : new InstalledExtensionLoader(_logging.Object, Configuration().Object);
        ExtensionLoadTracker.Reset();
        var first = Assert.Single(loader.GetInstallations(), info => info.Name == _name).Installation!;
        var second = Assert.Single(loader.GetInstallations(), info => info.Name == _name).Installation!;
        var rows = new List<string>();
        ExtensionLoadTracker.EmitSummary(rows.Add);
        Assert.Empty(rows);
        Assert.Equal(first.CacheKey, second.CacheKey);
        Assert.Equal(bundled ? "Bundled" : "Installed", first.Source);
        Assert.Equal(bundled ? 0 : 1, first.SourcePriority);
        Assert.Equal(0, first.RootPriority);
        Assert.Same(first.Load(), second.Load());
        Assert.NotEqual(first.CacheKey, Enumerate(bundled).Installation!.CacheKey);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void RemovedInstallationFailsEvenAfterItWasCached(bool bundled, bool warmCache)
    {
        Stage(bundled ? BundledDirectory : Path.Combine(_root, PackageId, "1.0.0"), Compute);
        var installation = Enumerate(bundled).Installation!;
        if (warmCache)
        {
            installation.Load();
        }
        File.Delete(installation.Location!);
        var error = Assert.Throws<FileNotFoundException>(() => installation.Load());
        Assert.Contains(_name, error.Message);
        Assert.Contains(installation.Location!, error.Message);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void ReplacementFailsForOldHandle_AndRediscoveryLoadsNewGeneration(bool bundled, bool warmCache)
    {
        var directory = bundled ? BundledDirectory : Path.Combine(_root, PackageId, "1.0.0");
        var path = Stage(directory, Compute);
        IExtensionLoader loader = bundled
            ? new BundledExtensionLoader(_logging.Object, Configuration().Object)
            : new InstalledExtensionLoader(_logging.Object, Configuration().Object);
        var original = Assert.Single(loader.GetInstallations(), info => info.Name == _name).Installation!;
        if (warmCache)
        {
            original.Load();
        }
        Stage(directory, Network);
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(5));
        Assert.Throws<InvalidOperationException>(() => original.Load());
        var replacement = Assert.Single(loader.GetInstallations(), info => info.Name == _name).Installation!;
        Assert.NotEqual(original.CacheKey, replacement.CacheKey);
        Assert.Equal(Network.GetName().Name, replacement.Load().GetName().Name);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CacheBypassStillValidatesAndLoadsFreshAssemblies(bool bundled)
    {
        Stage(bundled ? BundledDirectory : Path.Combine(_root, PackageId, "1.0.0"), Compute);
        var installation = Enumerate(bundled, bypass: true).Installation!;
        Assert.NotSame(installation.Load(), installation.Load());
        File.SetLastWriteTimeUtc(installation.Location!, File.GetLastWriteTimeUtc(installation.Location!).AddSeconds(5));
        Assert.Throws<InvalidOperationException>(() => installation.Load());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FileStampDetectsLengthAndTimestampChangesIndependently(bool changeLength)
    {
        var path = Stage(Path.Combine(_root, PackageId, "1.0.0"), Compute);
        var installation = Enumerate(bundled: false).Installation!;
        var timestamp = File.GetLastWriteTimeUtc(path);
        if (changeLength)
        {
            using (var file = new FileStream(path, FileMode.Append, FileAccess.Write))
            {
                file.WriteByte(0);
            }
            File.SetLastWriteTimeUtc(path, timestamp);
        }
        else
        {
            File.SetLastWriteTimeUtc(path, timestamp.AddSeconds(5));
        }
        Assert.Throws<InvalidOperationException>(installation.ValidateUnchanged);
    }

    [Fact]
    public void PinnedInstallationReadsItsOwnSidecarAndPayload()
    {
        var pinnedPath = Stage(Path.Combine(_root, PackageId, "1.0.0"), Compute);
        Stage(Path.Combine(_root, PackageId, "2.0.0"), Network);
        File.WriteAllText(Path.ChangeExtension(pinnedPath, ".commands.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1, packageId = PackageId, version = "1.0.0",
            verbs = new[] { "vm" }, leaves = new[] { "vm create" },
        }));
        var registry = new ExtensionRegistry();
        registry.Extensions[PackageId] = new ExtensionRegistryEntry { Version = "1.0.0", Pinned = true };
        var provider = new CLIAssemblyLoadingProvider(
            [new InstalledExtensionLoader(_logging.Object, Configuration().Object)],
            _logging.Object, new InMemoryRegistryStore(registry));
        var service = new AssemblyLoadingService(provider);
        var entry = Assert.Single(service.LoadAllExtensions()!);
        Assert.Equal("1.0.0", entry.Info.Version);
        Assert.Equal(pinnedPath, entry.Info.Location);
        ExtensionLoadTracker.Reset();
        var routing = Assert.Single(new ExtensionRoutingDiscovery(service, _logging.Object).Discover());
        Assert.Contains("vm create", routing.Leaves);
        var rows = new List<string>();
        ExtensionLoadTracker.EmitSummary(rows.Add);
        Assert.Empty(rows);
        Assert.Equal(Compute.GetName().Name, entry.Load().GetName().Name);
    }

    [Fact]
    public void ExactLoadDoesNotResolveVersionMetadataAgain()
    {
        Stage(BundledDirectory, Compute);
        var nupkg = Path.Combine(BundledDirectory, $"{PackageId}.1.0.0.nupkg");
        File.WriteAllText(nupkg, string.Empty);
        var installation = Enumerate(bundled: true).Installation!;
        Assert.Equal("1.0.0", installation.Version);
        File.Delete(nupkg);
        Assert.Equal(Compute.GetName().Name, installation.Load().GetName().Name);
    }

    [Fact]
    public void LegacyProviderAndDefaultServiceOverloadRemainSupported()
    {
        var provider = new Mock<IAssemblyLoadingProvider>(MockBehavior.Strict);
        provider.Setup(value => value.GetAllInstalledExtensions())
            .Returns([new InstalledExtensionRef(_name, "1.0.0", null)]);
        provider.Setup(value => value.LoadExtension(_name, "1.0.0")).Returns(Compute);
        var service = new AssemblyLoadingService(provider.Object);
        Assert.Same(Compute, Assert.Single(service.LoadAllExtensions()!).Load());

        IAssemblyLoadingService legacy = new LegacyService();
        var handle = new ExtensionInstallation(_name, "1.0.0", null, "Test", 0, 0, "test",
            () => Network, () => { });
        Assert.Same(Network, legacy.LoadExtension(handle));
    }

    [Fact]
    public void ExactFallbackFailureDoesNotRetryByName()
    {
        var handle = new ExtensionInstallation(_name, "1.0.0", null, "Test", 0, 0, "test",
            () => throw new InvalidOperationException("could not be found by any extension loader"),
            () => { });
        var provider = new Mock<IAssemblyLoadingProvider>(MockBehavior.Strict);
        provider.Setup(value => value.GetAllInstalledExtensions()).Returns(
            [new InstalledExtensionRef(_name, "1.0.0", null) { Installation = handle }]);
        var results = new ExtensionRoutingDiscovery(new AssemblyLoadingService(provider.Object), _logging.Object).Discover();
        Assert.Empty(results);
        provider.Verify(value => value.LoadExtension(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _logging.Verify(value => value.Warn(It.Is<string>(text => text.Contains(_name))), Times.Once);
    }

    [Fact]
    public void ValidSidecarWithHandleDoesNotLoadAssembly()
    {
        var path = Stage(Path.Combine(_root, PackageId, "1.0.0"), Compute);
        File.WriteAllText(Path.ChangeExtension(path, ".commands.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1, packageId = PackageId, version = "1.0.0",
            verbs = new[] { "vm" }, leaves = new[] { "vm create" },
        }));
        var provider = new Mock<IAssemblyLoadingProvider>(MockBehavior.Strict);
        provider.Setup(value => value.GetAllInstalledExtensions()).Returns([Enumerate(bundled: false)]);
        ExtensionLoadTracker.Reset();
        var result = new ExtensionRoutingDiscovery(new AssemblyLoadingService(provider.Object), _logging.Object).Discover();
        Assert.Single(result);
        var rows = new List<string>();
        ExtensionLoadTracker.EmitSummary(rows.Add);
        Assert.Empty(rows);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FullDiscoveryUsesExactDelegateAndIsolatesSameNameVersionIndices(bool transient)
    {
        Stage(BundledDirectory, Compute);
        File.WriteAllText(Path.Combine(BundledDirectory, $"{PackageId}.1.0.0.nupkg"), string.Empty);
        var installedPath = Stage(Path.Combine(_root, PackageId, "1.0.0"), Network);
        var bundled = Enumerate(bundled: true);
        var installed = Enumerate(bundled: false);
        var provider = new Mock<IAssemblyLoadingProvider>(MockBehavior.Strict);
        provider.Setup(value => value.GetAllInstalledExtensions()).Returns([bundled, installed]);
        using var engine = CreateEngine(new AssemblyLoadingService(provider.Object));
        for (var i = 0; i < 2; i++)
        {
            var results = await Discover(engine, transient);
            Assert.Equal(2, results.Count);
            Assert.Contains(Assert.Single(results, result => result.Location == bundled.Location).Commands.Keys, key => key == "vm create");
            Assert.DoesNotContain("vm create", Assert.Single(results, result => result.Location == installedPath).Commands.Keys);
            Assert.Equal(Network.GetName().Name, Assert.Single(results, result => result.Location == installedPath).PackageId);
        }
        File.Delete(installedPath);
        var afterRemoval = await Discover(engine, transient);
        Assert.Single(afterRemoval);
        Assert.Equal(bundled.Location, afterRemoval[0].Location);
        provider.Verify(value => value.LoadExtension(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    private Engine CreateEngine(IAssemblyLoadingService service) => new(
        new ClientMetadata(ClientType.CLI, "test"),
        new Mock<ISendAsyncTerminalFactory>().Object,
        new Mock<IProfileService>().Object,
        new Mock<IAuthService>().Object,
        new Mock<ITelemetryService>().Object,
        _logging.Object, service,
        new Mock<IArgumentParsingService>().Object,
        Configuration().Object,
        new Mock<IHttpPipelineService>().Object);

    private static async Task<List<DiscoveredExtensionInfo>> Discover(Engine engine, bool transient)
    {
        var results = new List<DiscoveredExtensionInfo>();
        OutputStreamDelegate noop = (_, _) => Task.CompletedTask;
        var streams = new OutputStreams((value, _) =>
        {
            results.AddRange(Assert.IsAssignableFrom<IList<DiscoveredExtensionInfo>>(value));
            return Task.CompletedTask;
        }, noop, noop, noop, noop, noop);
        using var session = engine.OpenDiscoverySession([], null, streams, isTransient: transient);
        await session.RunAsync();
        return results;
    }

    private sealed class LegacyService : IAssemblyLoadingService
    {
        public Assembly LoadExtension(string extensionName, string? extensionVersion = null)
            => throw new InvalidOperationException("Exact overload must not search by name.");
        public IList<(InstalledExtensionRef Info, Func<Assembly> Load)>? LoadAllExtensions() => null;
    }

    public void Dispose()
    {
        DirectoryUtils.TryDeleteDirectory(_root);
        DirectoryUtils.TryDeleteDirectory(BundledDirectory);
        ExtensionLoadTracker.Reset();
    }
}
