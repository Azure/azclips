// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Configuration.Options;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Extension;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Tests.Common.Stubs;

namespace Cli.Tests;

[Collection("Azclips Service Provider Tests")]
public sealed class ExtensionInstallerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "azclips-installer-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        AzclipsServiceProvider.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public async Task InstallExtensionAsync_ShouldThrowExtensionNotFound_WhenFeedHasNoMatchingPackage()
    {
        // An empty local folder feed has no packages, so the latest-version lookup misses and the
        // installer must report a typed ExtensionNotFound rather than an untyped exception.
        var emptyFeed = Path.Combine(_root, "feed");
        Directory.CreateDirectory(emptyFeed);

        var config = new Mock<IConfigurationService>();
        config.Setup(c => c.GetConfiguration<AssemblyLoadingOptions>())
            .Returns(new AssemblyLoadingOptions { InstalledExtensionsRoot = Path.Combine(_root, "installed") });
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services => services.AddSingleton(config.Object));
        AzclipsServiceProvider.Initialize(new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));

        var installer = new ExtensionInstaller(new NullLoggingService(), feedUrl: emptyFeed);

        var ex = await Assert.ThrowsAsync<AzclipsErrorException>(() =>
            installer.InstallExtensionAsync("Azclips.Missing.Package", version: null));

        Assert.Equal("ExtensionNotFound", ex.Code);
        Assert.Equal(4, ex.ExitCode);
        Assert.StartsWith("Could not find package Azclips.Missing.Package", ex.Message);
    }

    [Fact]
    public async Task InstallExtensionAsync_ShouldThrowExtensionNotFound_WhenExplicitVersionIsNotInFeed()
    {
        // An explicit --version skips the latest-version lookup and goes straight to download. When every
        // feed reports the package as not found, the installer must still return ExtensionNotFound.
        var emptyFeed = Path.Combine(_root, "feed");
        Directory.CreateDirectory(emptyFeed);

        var config = new Mock<IConfigurationService>();
        config.Setup(c => c.GetConfiguration<AssemblyLoadingOptions>())
            .Returns(new AssemblyLoadingOptions { InstalledExtensionsRoot = Path.Combine(_root, "installed") });
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services => services.AddSingleton(config.Object));
        AzclipsServiceProvider.Initialize(new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));

        var installer = new ExtensionInstaller(new NullLoggingService(), feedUrl: emptyFeed);

        var ex = await Assert.ThrowsAsync<AzclipsErrorException>(() =>
            installer.InstallExtensionAsync("Azclips.Missing.Package", version: "1.0.0"));

        Assert.Equal("ExtensionNotFound", ex.Code);
        Assert.Equal(4, ex.ExitCode);
        Assert.StartsWith("Could not find package Azclips.Missing.Package@1.0.0", ex.Message);
    }
}
