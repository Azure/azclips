// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;
using Core.AssemblyLoading;
using Core.Cli;
using Core.Extensions;
using Moq;

namespace Cli.Tests;

public sealed class AssemblyReferenceTests
{
    private sealed class Reference(string key, Action validate) : AssemblyReference(key, validate);

    [Fact]
    public void CliComparerOrdersMixedReferencesConsistently()
    {
        var bundled = new ExtensionInstallation("Widget", "1.0.0", "z.dll", "Bundled", 0, 0, "z",
            () => throw new InvalidOperationException("Must not load."), () => { });
        var installed = new ExtensionInstallation("Widget", "1.0.0", "a.dll", "Installed", 1, 0, "a",
            () => throw new InvalidOperationException("Must not load."), () => { });
        var other = new Reference("m", () => { });
        var comparer = CliAssemblyReferenceComparer.Instance;
        Assert.True(comparer.Compare(bundled, installed) < 0);
        Assert.True(comparer.Compare(installed, other) < 0);
        Assert.True(comparer.Compare(bundled, other) < 0);
        Assert.True(comparer.Compare(other, bundled) > 0);
        Assert.True(comparer.Compare(null, bundled) < 0);
        Assert.True(comparer.Compare(bundled, null) > 0);
        Assert.Equal(0, comparer.Compare(other, other));
    }

    [Fact]
    public void CliProviderRejectsUnsupportedReferenceWithoutSearchingLoaders()
    {
        var loader = new Mock<IExtensionLoader>(MockBehavior.Strict);
        var provider = new CLIAssemblyLoadingProvider([loader.Object],
            new Mock<ILoggingService>().Object, new Mock<IExtensionRegistryStore>().Object);
        Assert.Throws<ArgumentException>(() => provider.LoadExtension(new Reference("foreign", () => { })));
        loader.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactAndDeferredLoadsDelegateOpaqueReferencesToProvider(bool deferred)
    {
        var validationCount = 0;
        var reference = new Reference("client-owned-identity", () => validationCount++);
        var expected = typeof(AssemblyReferenceTests).Assembly;
        var provider = new Mock<IAssemblyLoadingProvider>(MockBehavior.Strict);
        provider.Setup(value => value.LoadExtension(reference)).Returns(() =>
        {
            Assert.Equal(1, validationCount);
            return expected;
        });
        provider.Setup(value => value.GetAllInstalledExtensions())
            .Returns([new InstalledExtensionRef("Widget", "1.0.0", null) { AssemblyReference = reference }]);
        var service = new AssemblyLoadingService(provider.Object);

        var load = deferred ? Assert.Single(service.LoadAllExtensions()!).Load
            : new Func<Assembly>(() => service.LoadExtension(reference));
        Assert.Equal(0, validationCount);
        Assert.Same(expected, load());
        Assert.Equal(2, validationCount);
        provider.Verify(value => value.LoadExtension(reference), Times.Once);
        provider.Verify(value => value.LoadExtension(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void InvalidReferenceFailsBeforeCallingProvider()
    {
        var reference = new Reference("invalid", () => throw new InvalidOperationException("No longer usable."));
        var provider = new Mock<IAssemblyLoadingProvider>(MockBehavior.Strict);
        var service = new AssemblyLoadingService(provider.Object);
        Assert.Throws<InvalidOperationException>(() => service.LoadExtension(reference));
        provider.VerifyNoOtherCalls();
    }

    [Fact]
    public void ReferenceInvalidatedDuringLoadFailsWithoutNameVersionRetry()
    {
        var changed = false;
        var reference = new Reference("changing", () =>
        {
            if (changed)
            {
                throw new InvalidOperationException("Changed during loading.");
            }
        });
        var provider = new Mock<IAssemblyLoadingProvider>(MockBehavior.Strict);
        provider.Setup(value => value.LoadExtension(reference)).Returns(() =>
        {
            changed = true;
            return typeof(AssemblyReferenceTests).Assembly;
        });
        var service = new AssemblyLoadingService(provider.Object);
        Assert.Throws<InvalidOperationException>(() => service.LoadExtension(reference));
        provider.Verify(value => value.LoadExtension(reference), Times.Once);
        provider.Verify(value => value.LoadExtension(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
