// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Extensions;
using Microsoft.Azure.Azclips.Extensions.Extension.Processors;
using Xunit;

namespace Cli.Tests;

public class ExtensionRemoveTests
{
    [Fact]
    public void BuildInstalledRegistryFromFilesystem_ShouldDiscoverPackageIdsAndOverlayPreferences()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"azclips-remove-test-{Guid.NewGuid():N}");
        var packageId = "Microsoft.Azure.Azclips.Extensions.Compute";
        var packageDir = Path.Combine(tempRoot, packageId);

        try
        {
            Directory.CreateDirectory(Path.Combine(packageDir, "0.1.0"));
            Directory.CreateDirectory(Path.Combine(packageDir, "0.2.0"));

            var fileRegistry = new ExtensionRegistry();
            fileRegistry.Extensions[packageId] = new ExtensionRegistryEntry
            {
                Version = "0.2.0",
                Pinned = true,
                Source = "https://example/feed",
                InstalledAt = new DateTimeOffset(2026, 6, 24, 0, 0, 0, TimeSpan.Zero),
            };

            var discovered = RemoveExtensionProcessor.BuildInstalledRegistryFromFilesystem(tempRoot, fileRegistry);

            Assert.True(discovered.Extensions.ContainsKey(packageId));
            var entry = discovered.Extensions[packageId];
            Assert.Equal("0.2.0", entry.Version);
            Assert.True(entry.Pinned);
            Assert.Equal("https://example/feed", entry.Source);
            Assert.Equal(new DateTimeOffset(2026, 6, 24, 0, 0, 0, TimeSpan.Zero), entry.InstalledAt);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void BuildInstalledRegistryFromFilesystem_ShouldSkipPackageDirectoryWithoutValidVersionFolder()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"azclips-remove-test-{Guid.NewGuid():N}");
        var packageId = "Microsoft.Azure.Azclips.Extensions.Invalid";
        var packageDir = Path.Combine(tempRoot, packageId);

        try
        {
            Directory.CreateDirectory(Path.Combine(packageDir, "not-a-version"));

            var discovered = RemoveExtensionProcessor.BuildInstalledRegistryFromFilesystem(tempRoot, new ExtensionRegistry());

            Assert.False(discovered.Extensions.ContainsKey(packageId));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void EnsurePackageDirectoryDeleted_ShouldDeleteExistingDirectory()
    {
        var packageId = "Microsoft.Azure.Azclips.Extensions.Compute";
        var tempRoot = Path.Combine(Path.GetTempPath(), $"azclips-remove-test-{Guid.NewGuid():N}");
        var packageDir = Path.Combine(tempRoot, packageId);

        try
        {
            Directory.CreateDirectory(packageDir);
            File.WriteAllText(Path.Combine(packageDir, "payload.dll"), "x");

            RemoveExtensionProcessor.EnsurePackageDirectoryDeleted(packageDir, packageId);

            Assert.False(Directory.Exists(packageDir));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void EnsurePackageDirectoryDeleted_ShouldThrow_WhenDeleteCallbackThrows()
    {
        var packageId = "Microsoft.Azure.Azclips.Extensions.Compute";
        var tempRoot = Path.Combine(Path.GetTempPath(), $"azclips-remove-test-{Guid.NewGuid():N}");
        var packageDir = Path.Combine(tempRoot, packageId);

        try
        {
            Directory.CreateDirectory(packageDir);

            var ex = Assert.Throws<AzclipsErrorException>(() =>
                RemoveExtensionProcessor.EnsurePackageDirectoryDeleted(
                    packageDir,
                    packageId,
                    _ => throw new IOException("locked")));

            Assert.Equal("ExtensionRemoveFailed", ex.Code);
            Assert.Equal(1, ex.ExitCode);
            Assert.True(Directory.Exists(packageDir));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void EnsurePackageDirectoryDeleted_ShouldSucceed_WhenDirectoryVanishesDuringDelete()
    {
        var packageId = "Microsoft.Azure.Azclips.Extensions.Compute";
        var tempRoot = Path.Combine(Path.GetTempPath(), $"azclips-remove-test-{Guid.NewGuid():N}");
        var packageDir = Path.Combine(tempRoot, packageId);

        try
        {
            Directory.CreateDirectory(packageDir);

            RemoveExtensionProcessor.EnsurePackageDirectoryDeleted(
                packageDir,
                packageId,
                _ => throw new DirectoryNotFoundException("gone"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void EnsurePackageDirectoryDeleted_ShouldThrow_WhenDirectoryStillExistsAfterDeleteAttempt()
    {
        var packageId = "Microsoft.Azure.Azclips.Extensions.Compute";
        var tempRoot = Path.Combine(Path.GetTempPath(), $"azclips-remove-test-{Guid.NewGuid():N}");
        var packageDir = Path.Combine(tempRoot, packageId);

        try
        {
            Directory.CreateDirectory(packageDir);

            var ex = Assert.Throws<AzclipsErrorException>(() =>
                RemoveExtensionProcessor.EnsurePackageDirectoryDeleted(
                    packageDir,
                    packageId,
                    _ => { }));

            Assert.Equal("ExtensionRemoveFailed", ex.Code);
            Assert.Contains("still exists", ex.Message);
            Assert.True(Directory.Exists(packageDir));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}

