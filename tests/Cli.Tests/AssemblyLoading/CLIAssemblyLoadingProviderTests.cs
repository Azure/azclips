// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;
using Core.Cli;
using Core.Extensions;
using Tests.Common.Utilities;

namespace Cli.Tests;

[Collection("Extension Load Tracker Tests")]
public sealed class CLIAssemblyLoadingProviderTests
{
    [Fact]
    public void GetAllInstalledExtensions_ShouldPreferPinnedVersion_FromRegistry()
    {
        var registry = new ExtensionRegistry();
        registry.Extensions["Microsoft.Azure.Azclips.Extensions.Compute"] = new ExtensionRegistryEntry
        {
            Version = "0.1.1",
            Pinned = true,
            InstalledAt = DateTimeOffset.UtcNow,
        };

        var loader = new StubLoader(("Compute", "0.1.2"));
        var provider = new CLIAssemblyLoadingProvider(
            [loader],
            new NullLoggingService(),
            new InMemoryRegistryStore(registry));

        var installed = provider.GetAllInstalledExtensions();

        var single = Assert.Single(installed!);
        Assert.Equal("Compute", single.Name);
        Assert.Equal("0.1.1", single.Version);
    }

    [Fact]
    public void GetAllInstalledExtensions_ShouldKeepLoaderVersion_WhenNotPinned()
    {
        var registry = new ExtensionRegistry();
        registry.Extensions["Microsoft.Azure.Azclips.Extensions.Compute"] = new ExtensionRegistryEntry
        {
            Version = "0.1.1",
            Pinned = false,
            InstalledAt = DateTimeOffset.UtcNow,
        };

        var loader = new StubLoader(("Compute", "0.1.2"));
        var provider = new CLIAssemblyLoadingProvider(
            [loader],
            new NullLoggingService(),
            new InMemoryRegistryStore(registry));

        var installed = provider.GetAllInstalledExtensions();

        var single = Assert.Single(installed!);
        Assert.Equal("Compute", single.Name);
        Assert.Equal("0.1.2", single.Version);
    }

    [Fact]
    public void LoadExtension_ShouldLoadSelectedLocation_ThroughLoaderThatReportedIt()
    {
        var bundledAssembly = typeof(string).Assembly;
        var installedAssembly = typeof(FactAttribute).Assembly;
        var bundled = new PathStubLoader("Compute", "0.1.3", @"C:\bundled\Compute.dll", bundledAssembly);
        var installed = new PathStubLoader("Compute", "0.1.3", @"C:\installed\Compute.dll", installedAssembly);
        var selected = new SelectedExtensionLocations();
        var provider = new CLIAssemblyLoadingProvider(
            [bundled, installed],
            new NullLoggingService(),
            new InMemoryRegistryStore(new ExtensionRegistry()),
            selected);
        provider.GetAllInstalledExtensions();
        selected.Record("Microsoft.Azure.Azclips.Extensions.Compute", "0.1.3", @"C:\installed\Compute.dll");

        var assembly = provider.LoadExtension("Compute", "0.1.3");

        Assert.Same(installedAssembly, assembly);
        Assert.Equal(0, bundled.SearchCount);
        Assert.Equal(@"C:\installed\Compute.dll", installed.LoadedPath);
    }

    [Fact]
    public void LoadExtension_ShouldSearchLoaders_WhenNothingSelected()
    {
        var bundledAssembly = typeof(string).Assembly;
        var bundled = new PathStubLoader("Compute", "0.1.3", @"C:\bundled\Compute.dll", bundledAssembly);
        var installed = new PathStubLoader("Compute", "0.1.3", @"C:\installed\Compute.dll", typeof(FactAttribute).Assembly);
        var provider = new CLIAssemblyLoadingProvider(
            [bundled, installed],
            new NullLoggingService(),
            new InMemoryRegistryStore(new ExtensionRegistry()),
            new SelectedExtensionLocations());

        Assert.Same(bundledAssembly, provider.LoadExtension("Compute", "0.1.3"));
    }

    [Fact]
    public void LoadExtension_ShouldSearchLoaders_WhenRequestedVersionDiffersFromSelection()
    {
        var bundledAssembly = typeof(string).Assembly;
        var bundled = new PathStubLoader("Compute", "0.2.0", @"C:\bundled\Compute.dll", bundledAssembly);
        var installed = new PathStubLoader("Compute", "0.1.3", @"C:\installed\Compute.dll", typeof(FactAttribute).Assembly);
        var selected = new SelectedExtensionLocations();
        var provider = new CLIAssemblyLoadingProvider(
            [bundled, installed],
            new NullLoggingService(),
            new InMemoryRegistryStore(new ExtensionRegistry()),
            selected);
        provider.GetAllInstalledExtensions();
        selected.Record("Compute", "0.1.3", @"C:\installed\Compute.dll");

        Assert.Same(bundledAssembly, provider.LoadExtension("Compute", "0.2.0"));
        Assert.Null(installed.LoadedPath);
    }

    [Fact]
    public void LoadExtension_ShouldFailWithoutSearching_WhenSelectedLocationCannotBeLoaded()
    {
        var bundled = new PathStubLoader("Compute", "0.1.3", @"C:\bundled\Compute.dll", typeof(string).Assembly);
        var installed = new PathStubLoader("Compute", "0.1.3", @"C:\installed\Compute.dll", assembly: null);
        var selected = new SelectedExtensionLocations();
        var provider = new CLIAssemblyLoadingProvider(
            [bundled, installed],
            new NullLoggingService(),
            new InMemoryRegistryStore(new ExtensionRegistry()),
            selected);
        provider.GetAllInstalledExtensions();
        selected.Record("Compute", "0.1.3", @"C:\installed\Compute.dll");

        var ex = Assert.Throws<InvalidOperationException>(() => provider.LoadExtension("Compute", "0.1.3"));

        Assert.Contains(@"C:\installed\Compute.dll", ex.Message);
        Assert.Equal(0, bundled.SearchCount);
    }

    [Theory]
    [InlineData("Compute", "0.1.3", true)]
    [InlineData("Microsoft.Azure.Azclips.Extensions.Compute", "0.1.3.0", true)]
    [InlineData("Compute", null, true)]
    [InlineData("Compute", "0.1.4", false)]
    [InlineData("Network", "0.1.3", false)]
    public void SelectedExtensionLocations_ShouldMatchByShortNameAndEquivalentVersion(string name, string? version, bool expected)
    {
        var selected = new SelectedExtensionLocations();
        selected.Record("Microsoft.Azure.Azclips.Extensions.Compute", "0.1.3", @"C:\installed\Compute.dll");

        Assert.Equal(expected, selected.TryGet(name, version, out _));
    }

    private sealed class PathStubLoader(string name, string version, string location, Assembly? assembly) : IExtensionLoader
    {
        public int SearchCount { get; private set; }

        public string? LoadedPath { get; private set; }

        public Assembly? LoadExtension(string extensionName, string? requestedVersion = null)
        {
            SearchCount++;
            return string.Equals(extensionName, name, StringComparison.OrdinalIgnoreCase) &&
                   (string.IsNullOrEmpty(requestedVersion) || requestedVersion == version)
                ? assembly
                : null;
        }

        public IEnumerable<(string Name, string Version, string? Location)> GetInstalledExtensions() =>
            [(name, version, location)];

        public Assembly? LoadExtensionFromPath(string extensionName, string? requestedVersion, string assemblyPath)
        {
            LoadedPath = assemblyPath;
            return assembly;
        }
    }

    private sealed class StubLoader(params (string Name, string Version)[] installed) : IExtensionLoader
    {
        public Assembly? LoadExtension(string extensionName, string? version = null) => null;

        public IEnumerable<(string Name, string Version, string? Location)> GetInstalledExtensions() =>
            installed.Select(entry => (entry.Name, entry.Version, (string?)null));
    }

    private sealed class NullLoggingService : ILoggingService
    {
        public void Log(string message) { }
        public void Debug(string message) { }
        public void Error(string message) { }
        public void Info(string message) { }
        public void Verbose(string message) { }
        public void Warn(string message) { }
    }
}
