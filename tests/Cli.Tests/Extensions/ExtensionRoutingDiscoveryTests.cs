// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json;
using Core.Abstractions;
using Core.Cli;
using Core.Cli.Exceptions;
using Core.Cli.Parser;
using Core.Extensions;
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
    private readonly Dictionary<string, int> _loads = new();

    private ExtensionInstallation AddInstallation(string source, string version, string[] leaves,
        int rootPriority = 0, bool omitSidecar = false, string[]? verbs = null)
    {
        var folder = Path.Combine(_root, source, rootPriority.ToString(), version);
        Directory.CreateDirectory(folder);
        var location = Path.Combine(folder, $"{Compute}.dll");
        var key = $"{source}:{rootPriority}:{version}";
        var installation = new ExtensionInstallation(
            "Compute", version, location, source, source == "Bundled" ? 0 : 1, rootPriority, key,
            () =>
            {
                _loads[key] = _loads.GetValueOrDefault(key) + 1;
                return typeof(Microsoft.Azure.Azclips.Extensions.Compute.ExtensionIndex).Assembly;
            }, () => { });
        if (!omitSidecar)
        {
            File.WriteAllText(Path.ChangeExtension(location, ".commands.json"), JsonSerializer.Serialize(new
            {
                schemaVersion = 1, packageId = Compute, version,
                verbs = verbs ?? ["vm"], leaves,
            }));
        }
        _installations.Add((new InstalledExtensionRef("Compute", version, location) { Installation = installation },
            installation.Load));
        return installation;
    }

    public ExtensionRoutingDiscoveryTests()
    {
        Directory.CreateDirectory(_root);
        _loader.Setup(service => service.LoadAllExtensions()).Returns(_installations);
    }

    private void Add(string package, string version, string[] verbs, string[] leaves,
        Func<Assembly>? load = null, string? invalidSidecar = null, bool omitSidecar = false)
    {
        var folder = Path.Combine(_root, package, version);
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

    private ServiceProvider CreateProvider(ExtensionRegistry? registry = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_loader.Object);
        services.AddSingleton(_logger.Object);
        services.AddCliExtensionDispatch();
        var store = new Mock<IExtensionRegistryStore>();
        store.Setup(value => value.Load()).Returns(registry ?? new ExtensionRegistry());
        services.AddSingleton(store.Object);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void DispatchBuildsRoutingFromSidecarsWithoutLoadingAnyExtension()
    {
        Add(Compute, "1.0.0", ["vm"], ["vm create"]);
        Add("Microsoft.Azure.Azclips.Extensions.Network", "1.0.0", ["network"], ["network show"]);
        using var provider = CreateProvider();
        var invocation = provider.GetRequiredService<ExtensionDispatcher>().Parse(["vm", "create"]);
        Assert.Equal("Compute", invocation.ModuleName);
        Assert.Equal("1.0.0", invocation.ModuleVersion);
        Assert.Equal("vm create", invocation.CommandName);
        _logger.Verify(log => log.Warn(It.IsAny<string>()), Times.Never);
        _logger.Verify(log => log.Debug(It.Is<string>(text => text.Contains("sidecars=2, DLL fallbacks=0"))), Times.Once);
        _logger.Verify(log => log.Debug(It.Is<string>(text => text.Contains("Built routing index"))), Times.Once);
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

    [Theory]
    [InlineData("vm aaa-probe")]
    [InlineData("vm zzz-probe")]
    public void EqualVersionDispatchCarriesBundledHandleRegardlessOfProbeSpelling(string probe)
    {
        AddInstallation("Installed", "1.0.0", ["vm create", probe]);
        var bundled = AddInstallation("Bundled", "1.0.0", ["vm create"]);
        var registry = new ExtensionRegistry();
        registry.Extensions[Compute] = new ExtensionRegistryEntry
        {
            Version = "1.0.0", Namespaces = ["vm"],
            Contributions = [new Contribution { Verb = "vm", Path = probe.Split(' ')[1] }],
        };
        using var provider = CreateProvider(registry);
        var dispatcher = provider.GetRequiredService<ExtensionDispatcher>();
        Assert.Same(bundled, dispatcher.Parse(["vm", "create"]).Installation);
        Assert.Throws<CommandNotFoundException>(() => dispatcher.Parse(probe.Split(' ')));
        Assert.Empty(_loads);
    }

    [Fact]
    public void IdenticalSidecarsRetainBundledWinnerAndItsHandle()
    {
        AddInstallation("Installed", "1.0.0", ["vm create"]);
        var bundled = AddInstallation("Bundled", "1.0.0", ["vm create"]);
        using var provider = CreateProvider();
        var invocation = provider.GetRequiredService<ExtensionDispatcher>().Parse(["vm", "create"]);
        Assert.Same(bundled, invocation.Installation);
        Assert.Empty(_loads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StaleRegistryCannotAddAnUnselectedNamespace(bool emptySurface)
    {
        AddInstallation("Installed", "1.0.0", ["disk aaa-probe"], verbs: ["disk"]);
        AddInstallation("Bundled", "1.0.0", emptySurface ? [] : ["vm create"], verbs: emptySurface ? [] : ["vm"]);
        var registry = new ExtensionRegistry();
        registry.Extensions[Compute] = new ExtensionRegistryEntry { Version = "1.0.0", Namespaces = ["disk"] };
        using var provider = CreateProvider(registry);
        Assert.Throws<CommandNotFoundException>(() =>
            provider.GetRequiredService<ExtensionDispatcher>().Parse(["disk", "aaa-probe"]));
        Assert.Empty(_loads);
    }

    [Theory]
    [InlineData("1.0.0", "2.0.0", false, "Installed")]
    [InlineData("2.0.0", "1.0.0", false, "Bundled")]
    [InlineData("2.0.0", "1.0.0", true, "Installed")]
    [InlineData("1.0.0", "1.0.0", true, "Bundled")]
    public void DispatchPreservesPinAndVersionBeforeSource(string bundledVersion, string installedVersion,
        bool pinned, string expectedSource)
    {
        var installed = AddInstallation("Installed", installedVersion, ["vm create"]);
        var bundled = AddInstallation("Bundled", bundledVersion, ["vm create"]);
        var registry = new ExtensionRegistry();
        registry.Extensions[Compute] = new ExtensionRegistryEntry { Version = installedVersion, Pinned = pinned };
        using var provider = CreateProvider(registry);
        var invocation = provider.GetRequiredService<ExtensionDispatcher>().Parse(["vm", "create"]);
        Assert.Same(expectedSource == "Bundled" ? bundled : installed, invocation.Installation);
        Assert.Equal(invocation.Installation!.Version, invocation.ModuleVersion);
        Assert.Empty(_loads);
    }

    [Fact]
    public void RootPriorityReachesInvocationWithoutMetadataTieBreak()
    {
        AddInstallation("Bundled", "1.0.0", ["vm create", "vm aaa"], rootPriority: 1);
        var preferred = AddInstallation("Bundled", "1.0.0", ["vm create", "vm zzz"]);
        using var provider = CreateProvider();
        Assert.Same(preferred, provider.GetRequiredService<ExtensionDispatcher>().Parse(["vm", "zzz"]).Installation);
        Assert.Empty(_loads);
    }

    [Fact]
    public void DllFallbackRetainsOnlyItsEnumeratedHandle()
    {
        var installed = AddInstallation("Installed", "2.0.0", [], omitSidecar: true);
        AddInstallation("Bundled", "1.0.0", ["vm create"]);
        using var provider = CreateProvider();
        var invocation = provider.GetRequiredService<ExtensionDispatcher>().Parse(["vm", "create"]);
        Assert.Same(installed, invocation.Installation);
        Assert.Equal(1, Assert.Single(_loads).Value);
        Assert.Equal(installed.CacheKey, Assert.Single(_loads).Key);
    }

    [Fact]
    public void RootLeafAndMultipleNamespacesRetainHandle()
    {
        var handle = AddInstallation("Bundled", "1.0.0", ["login", "vm create"], verbs: ["login", "vm"]);
        using var provider = CreateProvider();
        var dispatcher = provider.GetRequiredService<ExtensionDispatcher>();
        Assert.Same(handle, dispatcher.Parse(["login"]).Installation);
        Assert.Same(handle, dispatcher.Parse(["vm", "create"]).Installation);
        Assert.Empty(_loads);
    }

    [Fact]
    public void SharedVerbRoutesEachPackageWithItsOwnProvenance()
    {
        var handle = AddInstallation("Bundled", "1.0.0", ["vm create"]);
        Add("Contoso.Extra", "1.0.0", ["vm"], ["vm inspect"]);
        using var provider = CreateProvider();
        var dispatcher = provider.GetRequiredService<ExtensionDispatcher>();
        Assert.Same(handle, dispatcher.Parse(["vm", "create"]).Installation);
        var extra = dispatcher.Parse(["vm", "inspect"]);
        Assert.Equal("Contoso.Extra", extra.ExtensionName);
        Assert.Null(extra.Installation);
        Assert.Empty(_loads);
    }

    [Fact]
    public void InstallationPriorityDoesNotSuppressConflictsBetweenPackages()
    {
        AddInstallation("Bundled", "1.0.0", ["vm create"]);
        Add("Contoso.Extra", "1.0.0", ["vm"], ["vm create"]);
        using var provider = CreateProvider();
        Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<ExtensionDispatcher>().Parse(["vm", "create"]));
        Assert.Empty(_loads);
    }

    [Fact]
    public void EnumerationFailurePreservesWarnedUnresolvedRegistryFallback()
    {
        _loader.Setup(service => service.LoadAllExtensions()).Throws(new IOException("Enumeration unavailable"));
        var registry = new ExtensionRegistry();
        registry.Extensions[Compute] = new ExtensionRegistryEntry
        {
            Version = "1.0.0",
            Contributions = [new Contribution { Verb = "vm", Path = "create" }],
        };
        using var provider = CreateProvider(registry);
        var invocation = provider.GetRequiredService<ExtensionDispatcher>().Parse(["vm", "create"]);
        Assert.Null(invocation.Installation);
        Assert.Equal("Compute", invocation.ExtensionName);
        _logger.Verify(log => log.Warn(It.Is<string>(text => text.Contains("falling back to the current registry"))), Times.Once);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
