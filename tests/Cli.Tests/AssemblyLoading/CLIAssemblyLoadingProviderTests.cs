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
    public void GetAllInstalledExtensions_ShouldReportEveryCopy_WithoutChoosing()
    {
        var loader = new StubLoader(("Compute", "0.1.2"), ("Compute", "0.1.1"));
        var provider = new CLIAssemblyLoadingProvider(
            [loader],
            new NullLoggingService());

        var installed = provider.GetAllInstalledExtensions();

        Assert.Equal(["0.1.2", "0.1.1"], installed!.Select(entry => entry.Version));
    }

    [Fact]
    public void GetAllInstalledExtensions_ShouldEnumerateBundledFirst_RegardlessOfRegistrationOrder()
    {
        var installed = new StubLoader(("Compute", "0.1.3")) { Tier = ExtensionTier.Installed };
        var bundled = new StubLoader(("Compute", "0.1.3")) { Tier = ExtensionTier.Bundled, Location = @"C:\bundled\Compute.dll" };
        var provider = new CLIAssemblyLoadingProvider(
            [installed, bundled],
            new NullLoggingService());

        var reported = provider.GetAllInstalledExtensions();

        Assert.Equal([@"C:\bundled\Compute.dll", null], reported!.Select(entry => entry.Location));
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

    [Fact]
    public void LoadExtensionAt_ShouldLoadThatLocation_EvenWhenAnotherCopyWouldWinASearch()
    {
        var bundled = new PathStubLoader("Compute", "0.1.3", @"C:\bundled\Compute.dll", typeof(string).Assembly);
        var installedAssembly = typeof(FactAttribute).Assembly;
        var installed = new PathStubLoader("Compute", "0.1.3", @"C:\installed\Compute.dll", installedAssembly);
        var provider = new CLIAssemblyLoadingProvider(
            [bundled, installed],
            new NullLoggingService());
        provider.GetAllInstalledExtensions();

        Assert.Same(installedAssembly, provider.LoadExtensionAt("Compute", "0.1.3", @"C:\installed\Compute.dll"));
        Assert.Equal(0, bundled.SearchCount);
        Assert.Null(bundled.LoadedPath);
    }

    [Fact]
    public void LoadExtensionAt_ShouldFail_WhenNoLoaderReportedTheLocation()
    {
        var bundled = new PathStubLoader("Compute", "0.1.3", @"C:\bundled\Compute.dll", typeof(string).Assembly);
        var provider = new CLIAssemblyLoadingProvider(
            [bundled],
            new NullLoggingService());
        provider.GetAllInstalledExtensions();

        Assert.Throws<InvalidOperationException>(() => provider.LoadExtensionAt("Compute", "0.1.3", @"C:\elsewhere\Compute.dll"));
        Assert.Equal(0, bundled.SearchCount);
    }

    [Fact]
    public void SelectedExtensionLocations_ShouldListEverySelectedPackage_InOrder_IncludingThoseWithoutLocation()
    {
        var selected = new SelectedExtensionLocations();
        selected.Record("Microsoft.Azure.Azclips.Extensions.Network", "1.0.0", @"C:\bundled\Network.dll");
        selected.Record("Microsoft.Azure.Azclips.Extensions.Compute", "0.1.3", location: null);

        Assert.Equal(
            ["Microsoft.Azure.Azclips.Extensions.Network", "Microsoft.Azure.Azclips.Extensions.Compute"],
            selected.PackageIds);
        Assert.False(selected.TryGet("Compute", "0.1.3", out _));

        selected.Clear();
        Assert.Empty(selected.PackageIds);
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
        public ExtensionTier Tier { get; init; } = ExtensionTier.Installed;

        public string? Location { get; init; }

        public Assembly? LoadExtension(string extensionName, string? version = null) => null;

        public IEnumerable<(string Name, string Version, string? Location)> GetInstalledExtensions() =>
            installed.Select(entry => (entry.Name, entry.Version, Location));
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
