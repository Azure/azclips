// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;
using Core.Cli;
using Core.Extensions;
using Tests.Common.Utilities;

namespace Cli.Tests;

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
