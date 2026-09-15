// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Extensions;
using Microsoft.Azure.Azclips.Extensions.Extension.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Regression: pin/unpin must not lose the entry's <see cref="ExtensionRegistryEntry.Contributions"/>
/// or <see cref="ExtensionRegistryEntry.Namespaces"/>. Before the fix, <c>PinHelper.SetPinned</c>
/// rewrote the entry without copying those lists; on the next <c>Save()</c>, NormalizeEntry would
/// synthesize a single <c>(verb, "")</c> contribution from the legacy <c>Verb</c> field, erasing
/// every non-root leaf.
/// </summary>
public class ExtensionPinTests
{
    private const string PackageId = "Microsoft.Azure.Azclips.Extensions.Resources";

    private static ExtensionRegistry RegistryWithMultiLeafExtension(bool pinned)
    {
        var registry = new ExtensionRegistry();
        registry.Extensions[PackageId] = new ExtensionRegistryEntry
        {
            Verb = "deployment",
            Version = "1.0.0",
            Pinned = pinned,
            Source = "https://example/feed",
            InstalledAt = DateTimeOffset.UtcNow,
            Contributions =
            {
                new Contribution { Verb = "deployment", Path = "group create" },
                new Contribution { Verb = "deployment", Path = "group list" },
                new Contribution { Verb = "group", Path = "create" },
            },
            Namespaces = { "deployment", "group" },
        };
        return registry;
    }

    [Fact]
    public void ApplyPinChange_ShouldPreserveContributions_WhenPinning()
    {
        var registry = RegistryWithMultiLeafExtension(pinned: false);

        var (packageId, updated, mutated) = PinHelper.ApplyPinChange(registry, name: "Resources", pinned: true);

        Assert.True(mutated);
        Assert.Equal(PackageId, packageId);
        Assert.True(updated.Pinned);
        Assert.Equal(3, updated.Contributions.Count);
        Assert.Contains(updated.Contributions, c => c.Verb == "deployment" && c.Path == "group create");
        Assert.Contains(updated.Contributions, c => c.Verb == "deployment" && c.Path == "group list");
        Assert.Contains(updated.Contributions, c => c.Verb == "group" && c.Path == "create");
        Assert.Equal(new[] { "deployment", "group" }, updated.Namespaces);
    }

    [Fact]
    public void ApplyPinChange_ShouldPreserveContributions_WhenUnpinning()
    {
        var registry = RegistryWithMultiLeafExtension(pinned: true);

        var (_, updated, mutated) = PinHelper.ApplyPinChange(registry, name: "Resources", pinned: false);

        Assert.True(mutated);
        Assert.False(updated.Pinned);
        Assert.Equal(3, updated.Contributions.Count);
        Assert.Equal(new[] { "deployment", "group" }, updated.Namespaces);
    }

    [Fact]
    public void ApplyPinChange_ShouldReturnNotMutated_WhenAlreadyInDesiredState()
    {
        var registry = RegistryWithMultiLeafExtension(pinned: true);

        var (_, _, mutated) = PinHelper.ApplyPinChange(registry, name: PackageId, pinned: true);

        Assert.False(mutated);
        // And the existing entry's contributions are still intact (unchanged reference).
        Assert.Equal(3, registry.Extensions[PackageId].Contributions.Count);
    }

    [Fact]
    public void ApplyPinChange_ShouldThrowExtensionNotFound_WhenNoMatch()
    {
        var registry = RegistryWithMultiLeafExtension(pinned: false);

        var ex = Assert.Throws<AzclipsErrorException>(
            () => PinHelper.ApplyPinChange(registry, name: "no-such-extension", pinned: true));

        Assert.Equal("ExtensionNotFound", ex.Code);
    }

    [Fact]
    public void ApplyPinChange_ShouldRejectVerbTokens_AndIncludeSuggestion()
    {
        // Verbs/namespaces are sub-units of a package; accepting them as pin selectors would
        // let `extension pin --name deployment` silently pin the whole Resources package.
        // Instead, the lookup misses and the error message suggests the package short name.
        var registry = RegistryWithMultiLeafExtension(pinned: false);

        var ex = Assert.Throws<AzclipsErrorException>(
            () => PinHelper.ApplyPinChange(registry, name: "deployment", pinned: true));

        Assert.Equal("ExtensionNotFound", ex.Code);
        Assert.Contains("--name Resources", ex.Message);
    }

    [Fact]
    public void ApplyPinChange_NewEntry_ShouldNotShareContributionsList_WithExistingEntry()
    {
        // Defense-in-depth: rewriting the entry copies the contributions list rather than
        // aliasing it, so a future caller mutating the new entry doesn't bleed into the old.
        var registry = RegistryWithMultiLeafExtension(pinned: false);
        var originalContributions = registry.Extensions[PackageId].Contributions;

        PinHelper.ApplyPinChange(registry, name: "Resources", pinned: true);

        Assert.NotSame(originalContributions, registry.Extensions[PackageId].Contributions);
    }

    [Fact]
    public void ApplyPinChange_RoundTripThroughStore_ShouldPersistPreferencesOnly()
    {
        // Registry writes are preferences-only in §5.2+, so contributions/namespaces are
        // intentionally dropped during Save(). This check verifies pin state survives while
        // installed-state metadata is not persisted.
        var tempPath = Path.Combine(Path.GetTempPath(), $"azclips-pin-test-{Guid.NewGuid():N}.json");
        try
        {
            var registry = RegistryWithMultiLeafExtension(pinned: false);
            PinHelper.ApplyPinChange(registry, name: "Resources", pinned: true);

            new FileExtensionRegistryStore(tempPath).Save(registry);
            var reloaded = new FileExtensionRegistryStore(tempPath).Load();

            var entry = reloaded.Extensions[PackageId];
            Assert.True(entry.Pinned);
            Assert.Empty(entry.Contributions);
            Assert.Empty(entry.Namespaces);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}

