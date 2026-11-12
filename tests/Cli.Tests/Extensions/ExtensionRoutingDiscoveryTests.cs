// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json;
using Core.Abstractions;
using Core.Cli;
using Core.Cli.Exceptions;
using Core.Cli.Parser;
using Core.Extensions;
using Core.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cli.Tests;

public sealed class ExtensionRoutingDiscoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"routing-discovery-{Guid.NewGuid():N}");
    private readonly Mock<IAssemblyLoadingService> _loader = new(MockBehavior.Strict);
    private readonly Mock<ILoggingService> _logger = new();
    private readonly List<(InstalledExtensionRef Info, Func<Assembly> Load)> _installations = [];
    private const string Compute = "Microsoft.Azure.Azclips.Extensions.Compute";

    public ExtensionRoutingDiscoveryTests()
    {
        Directory.CreateDirectory(_root);
        _loader.Setup(service => service.LoadAllExtensions()).Returns(_installations);
    }

    private void Add(string package, string version, string[] verbs, string[] leaves,
        Func<Assembly>? load = null, string? invalidSidecar = null, bool omitSidecar = false, string tier = "")
    {
        var folder = Path.Combine(_root, tier, package, version);
        Directory.CreateDirectory(folder);
        var location = Path.Combine(folder, $"{package}.dll");
        if (!omitSidecar)
        {
            File.WriteAllText(Path.ChangeExtension(location, ".commands.json"),
                invalidSidecar ?? JsonSerializer.Serialize(new
                {
                    schemaVersion = 1, packageId = package, version, verbs, leaves,
                }));
        }
        _installations.Add((new InstalledExtensionRef(package, version, location),
            load ?? (() => throw new InvalidOperationException($"Unexpected assembly load: {package}"))));
    }

    private ServiceProvider CreateProvider(ExtensionRegistry? registry = null, CommandTimingRecorder? performance = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_loader.Object);
        services.AddSingleton(_logger.Object);
        if (performance is not null)
        {
            services.AddSingleton(performance);
        }
        services.AddCliExtensionDispatch();
        var store = new Mock<IExtensionRegistryStore>();
        store.Setup(value => value.Load()).Returns(registry ?? new ExtensionRegistry());
        services.AddSingleton(store.Object);
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DispatchBuildsRoutingFromSidecarsWithoutLoadingAnyExtension(bool timingEnabled)
    {
        Add(Compute, "1.0.0", ["vm"], ["vm create"]);
        Add("Microsoft.Azure.Azclips.Extensions.Network", "1.0.0", ["network"], ["network show"]);
        var performance = timingEnabled ? new CommandTimingRecorder() : null;
        using var provider = CreateProvider(performance: performance);
        var invocation = provider.GetRequiredService<ExtensionDispatcher>().Parse(["vm", "create"]);
        Assert.Equal("Compute", invocation.ModuleName);
        Assert.Equal("1.0.0", invocation.ModuleVersion);
        Assert.Equal("vm create", invocation.CommandName);
        _logger.Verify(log => log.Warn(It.IsAny<string>()), Times.Never);
        _logger.Verify(log => log.Debug(It.Is<string>(text => text.Contains("sidecars=2, DLL fallbacks=0"))), Times.Once);
        _logger.Verify(log => log.Debug(It.Is<string>(text => text.Contains("Built routing index"))), Times.Once);
        if (performance is not null)
        {
            var routing = Assert.Single(performance.Capture().Measurements);
            Assert.Equal("Routing discovery/selection/index", routing.Name);
            Assert.True(routing.IsComplete);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{")]
    [InlineData("{\"schemaVersion\":99}")]
    [InlineData("{\"schemaVersion\":1,\"packageId\":\"Wrong\"}")]
    [InlineData("{\"schemaVersion\":1,\"packageId\":\"Microsoft.Azure.Azclips.Extensions.Compute\",\"version\":\"9.0.0\"}")]
    public void BadSidecarFallsBackOnlyForItsExtension(string? invalidSidecar)
    {
        var loads = 0;
        Add(Compute, "1.0.0", ["vm"], ["vm create"], load: () =>
        {
            loads++;
            return typeof(Microsoft.Azure.Azclips.Extensions.Compute.ExtensionIndex).Assembly;
        }, invalidSidecar: invalidSidecar, omitSidecar: invalidSidecar is null);
        Add("Microsoft.Azure.Azclips.Extensions.Network", "1.0.0", ["network"], ["network show"]);
        using var provider = CreateProvider();
        var invocation = provider.GetRequiredService<ExtensionDispatcher>().Parse(["vm", "create"]);
        Assert.Equal("Compute", invocation.ModuleName);
        Assert.Equal(1, loads);
        _logger.Verify(log => log.Debug(It.Is<string>(text => text.Contains("sidecars=1, DLL fallbacks=1, failures=0"))), Times.Once);
    }

    [Fact]
    public void FailedFallbackWarnsAndDoesNotDiscardOtherExtensions()
    {
        Add(Compute, "1.0.0", ["vm"], ["vm create"]);
        Add("Broken", "1.0.0", [], [], omitSidecar: true);
        using var provider = CreateProvider();
        Assert.Equal("Compute", provider.GetRequiredService<ExtensionDispatcher>().Parse(["vm", "create"]).ModuleName);
        _logger.Verify(log => log.Warn(It.Is<string>(text => text.Contains("Failed to discover extension 'Broken'"))), Times.Once);
    }

    [Fact]
    public void InvalidUnicodeFallsBackWithoutLosingUnrelatedCommands()
    {
        var loads = 0;
        Add(Compute, "1.0.0", ["vm"], ["vm create"], load: () =>
        {
            loads++;
            return typeof(Microsoft.Azure.Azclips.Extensions.Compute.ExtensionIndex).Assembly;
        }, invalidSidecar:
            "{\"schemaVersion\":1,\"packageId\":\"Microsoft.Azure.Azclips.Extensions.Compute\",\"version\":\"1.0.0\",\"verbs\":[\"vm\\uD800\"],\"leaves\":[\"vm create\"]}");
        Add("Microsoft.Azure.Azclips.Extensions.Network", "1.0.0", ["network"], ["network show"]);
        using var provider = CreateProvider(new ExtensionRegistry());
        var dispatcher = provider.GetRequiredService<ExtensionDispatcher>();

        Assert.Equal("Network", dispatcher.Parse(["network", "show"]).ModuleName);
        Assert.Equal("Compute", dispatcher.Parse(["vm", "create"]).ModuleName);
        Assert.Equal(1, loads);
        _logger.Verify(log => log.Debug(It.Is<string>(text =>
            text.Contains("DLL fallback") && text.Contains("InvalidOperationException"))), Times.Once);
        _logger.Verify(log => log.Debug(It.Is<string>(text =>
            text.Contains("sidecars=1, DLL fallbacks=1, failures=0"))), Times.Once);
        _logger.Verify(log => log.Warn(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(false, "2.0.0", "delete")]
    [InlineData(true, "1.0.0", "create")]
    public void PreservesHighestVersionAndPinSelection(bool pinned, string expectedVersion, string command)
    {
        Add(Compute, "1.0.0", ["vm"], ["vm create"]);
        Add(Compute, "2.0.0", ["vm"], ["vm delete"]);
        var registry = new ExtensionRegistry();
        registry.Extensions[Compute] = new ExtensionRegistryEntry { Version = "1.0.0", Pinned = pinned };
        using var provider = CreateProvider(registry);
        var invocation = provider.GetRequiredService<ExtensionDispatcher>().Parse(["vm", command]);
        Assert.Equal(expectedVersion, invocation.ModuleVersion);
    }

    [Fact]
    public void PreservesCommandNotFoundForKnownVerb()
    {
        Add(Compute, "1.0.0", ["vm"], ["vm create"]);
        using var provider = CreateProvider();
        Assert.Throws<CommandNotFoundException>(() =>
            provider.GetRequiredService<ExtensionDispatcher>().Parse(["vm", "typo"]));
    }

    [Fact]
    public void PreservesExtensionNotInstalledForHintedCommand()
    {
        Add("Microsoft.Azure.Azclips.Extensions.Network", "1.0.0", ["network"], ["network show"]);
        using var provider = CreateProvider();
        Assert.Throws<ExtensionNotInstalledException>(() =>
            provider.GetRequiredService<ExtensionDispatcher>().Parse(["vm", "create"]));
    }

    [Fact]
    public void SharedVerbsRouteDistinctLeavesToTheirOwners()
    {
        Add(Compute, "1.0.0", ["vm"], ["vm create"]);
        Add("Contoso.Extra", "1.0.0", ["vm"], ["vm inspect"]);
        using var provider = CreateProvider();
        var dispatcher = provider.GetRequiredService<ExtensionDispatcher>();
        Assert.Equal("Compute", dispatcher.Parse(["vm", "create"]).ModuleName);
        Assert.Equal("Contoso.Extra", dispatcher.Parse(["vm", "inspect"]).ModuleName);
    }

    [Fact]
    public void UnresolvablePinStillReportsDrift()
    {
        Add(Compute, "2.0.0", ["vm"], ["vm create"]);
        var registry = new ExtensionRegistry();
        registry.Extensions[Compute] = new ExtensionRegistryEntry { Version = "1.0.0", Pinned = true };
        using var provider = CreateProvider(registry);
        Assert.Equal("2.0.0", provider.GetRequiredService<ExtensionDispatcher>().Parse(["vm", "create"]).ModuleVersion);
        _logger.Verify(log => log.Error(It.Is<string>(text =>
            text.Contains("[Pinned extension]") && text.Contains("1.0.0"))), Times.Once);
    }

    [Fact]
    public void FallbackRetriesUnversionedLoadWhenVersionCannotBeResolved()
    {
        _installations.Add((new InstalledExtensionRef("Compute", "1.0.0", null),
            () => throw new InvalidOperationException("Extension 'Compute' could not be found by any extension loader.")));
        _loader.Setup(service => service.LoadExtension("Compute", string.Empty))
            .Returns(typeof(Microsoft.Azure.Azclips.Extensions.Compute.ExtensionIndex).Assembly);
        var infos = new ExtensionRoutingDiscovery(_loader.Object, _logger.Object).Discover();
        var info = Assert.Single(infos);
        Assert.Equal("1.0.0", info.ResolvedVersion);
        Assert.Contains("vm create", info.Leaves);
        _loader.Verify(service => service.LoadExtension("Compute", string.Empty), Times.Once);
        _logger.Verify(log => log.Debug(It.Is<string>(text =>
            text.Contains("DLL fallback") && text.Contains("no assembly location"))), Times.Once);
    }

    [Fact]
    public void FallbackLoadsTheEnumeratedLocation_WhenALocationLoaderIsAvailable()
    {
        Add(Compute, "1.0.0", ["vm"], ["vm create"], invalidSidecar: "{");
        var location = _installations[0].Info.Location!;
        var calls = new List<(string Name, string? Version, string Location)>();
        var infos = new ExtensionRoutingDiscovery(_loader.Object, _logger.Object, (name, version, path) =>
        {
            calls.Add((name, version, path));
            return typeof(Microsoft.Azure.Azclips.Extensions.Compute.ExtensionIndex).Assembly;
        }).Discover();

        var info = Assert.Single(infos);
        Assert.Contains("vm create", info.Leaves);
        Assert.Equal(location, info.Location);
        Assert.Equal((Compute, (string?)"1.0.0", location), Assert.Single(calls));
    }

    [Fact]
    public void EqualVersionsSelectTheFirstEnumeratedCopy_AndRecordItForExecutionHelpAndCompletions()
    {
        Add(Compute, "1.0.0", ["vm"], ["vm create"], tier: "bundled");
        Add(Compute, "1.0.0", ["disk", "vm"], ["disk aaa-probe", "vm create"], tier: "installed");
        using var provider = CreateProvider();
        var dispatcher = provider.GetRequiredService<ExtensionDispatcher>();
        var selected = provider.GetRequiredService<SelectedExtensionLocations>();

        var packageIds = CliExtensionServiceRegistration.GetRoutingSelectedPackageIds(dispatcher, selected);

        Assert.Equal([Compute], packageIds);
        Assert.True(selected.TryGet("Compute", "1.0.0", out var location));
        Assert.Equal(_installations[0].Info.Location, location);
        Assert.Equal("Compute", dispatcher.Parse(["vm", "create"]).ModuleName);
        Assert.ThrowsAny<Exception>(() => dispatcher.Parse(["disk", "aaa-probe"]));
    }

    [Fact]
    public void EmptyContractsDoNotLoadAssemblies()
    {
        Add(Compute, "1.0.0", [], []);
        var infos = new ExtensionRoutingDiscovery(_loader.Object, _logger.Object).Discover();
        Assert.Empty(Assert.Single(infos).Leaves);
        _logger.Verify(log => log.Warn(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ConflictingPackagesStillFail()
    {
        Add(Compute, "1.0.0", ["vm"], ["vm create"]);
        Add("Contoso.Fork", "1.0.0", ["vm"], ["vm create"]);
        using var provider = CreateProvider();
        Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<ExtensionDispatcher>().Parse(["vm", "create"]));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
