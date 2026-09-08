// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Extensions;
using Microsoft.Azure.Azclips.Extensions.Extension.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Covers <see cref="ListExtensionsProcessor.BuildCandidates"/> and <see cref="ListExtensionsProcessor.InferTier"/>.
/// Direct unit tests over the projection helpers — no discovery session or DI required.
/// </summary>
public class ListExtensionsCandidatesTests
{
    private const string BundledRoot = @"C:\Program Files\azclips\Extensions";
    private const string InstalledRoot = @"C:\Users\user\.azclips\extensions";

    [Fact]
    public void InferTier_ShouldReturnInstalled_WhenLocationIsUnderInstalledRoot()
    {
        var location = Path.Combine(InstalledRoot, "Microsoft.Azure.Azclips.Extensions.Compute", "1.0.0", "dll.dll");
        Assert.Equal("Installed", ListExtensionsProcessor.InferTier(location, InstalledRoot));
    }

    [Fact]
    public void InferTier_ShouldReturnBundled_WhenLocationIsOutsideInstalledRoot()
    {
        var location = Path.Combine(BundledRoot, "Microsoft.Azure.Azclips.Extensions.Accounts", "Microsoft.Azure.Azclips.Extensions.Accounts.dll");
        Assert.Equal("Bundled", ListExtensionsProcessor.InferTier(location, InstalledRoot));
    }

    [Fact]
    public void InferTier_ShouldReturnBundled_WhenLocationIsNullOrEmpty()
    {
        Assert.Equal("Bundled", ListExtensionsProcessor.InferTier(null, InstalledRoot));
        Assert.Equal("Bundled", ListExtensionsProcessor.InferTier(string.Empty, InstalledRoot));
    }

    [Fact]
    public void InferTier_ShouldReturnBundled_WhenLocationSharesPrefixButIsNotUnderInstalledRoot()
    {
        // Sibling directory that merely shares a prefix must not classify as Installed. The
        // boundary check keeps '.../extensions' from matching '.../extensions-old/...'.
        var sibling = InstalledRoot + "-old";
        var location = Path.Combine(sibling, "Microsoft.Azure.Azclips.Extensions.Compute", "1.0.0", "dll.dll");
        Assert.Equal("Bundled", ListExtensionsProcessor.InferTier(location, InstalledRoot));
    }

    [Fact]
    public void BuildCandidates_ShouldReturnSingleElement_ForSingleTierCandidate()
    {
        var info = MakeInfo("Contoso.Foo", "1.0.0", Path.Combine(BundledRoot, "Contoso.Foo", "Contoso.Foo.dll"));
        var selection = new SelectedExtensionCandidate(info, [], ExtensionSelectionReason.SingleCandidate);
        var entry = new ExtensionRegistryEntry { Version = info.ResolvedVersion, Location = info.Location };

        var candidates = ListExtensionsProcessor.BuildCandidates(selection, entry, InstalledRoot);

        Assert.Single(candidates);
        Assert.Equal("1.0.0", candidates[0].Version);
        Assert.Equal("Bundled", candidates[0].Tier);
        Assert.Equal(info.Location, candidates[0].Location);
    }

    [Fact]
    public void BuildCandidates_ShouldSortDescending_AcrossBundledAndInstalled()
    {
        var bundled = MakeInfo("Contoso.Foo", "1.0.0", Path.Combine(BundledRoot, "Contoso.Foo", "Contoso.Foo.dll"));
        var installedHigher = MakeInfo("Contoso.Foo", "1.1.0", Path.Combine(InstalledRoot, "Contoso.Foo", "1.1.0", "Contoso.Foo.dll"));
        var selection = new SelectedExtensionCandidate(installedHigher, [bundled], ExtensionSelectionReason.HigherVersion);
        var entry = new ExtensionRegistryEntry { Version = installedHigher.ResolvedVersion, Location = installedHigher.Location };

        var candidates = ListExtensionsProcessor.BuildCandidates(selection, entry, InstalledRoot);

        Assert.Equal(2, candidates.Count);
        Assert.Equal("1.1.0", candidates[0].Version);
        Assert.Equal("Installed", candidates[0].Tier);
        Assert.Equal("1.0.0", candidates[1].Version);
        Assert.Equal("Bundled", candidates[1].Tier);
    }

    [Fact]
    public void BuildCandidates_ShouldSurfaceBundledWinner_WhenPinSelectsOlderBundledOverInstalled()
    {
        var bundledPinned = MakeInfo("Contoso.Foo", "1.0.0", Path.Combine(BundledRoot, "Contoso.Foo", "Contoso.Foo.dll"));
        var installedNewer = MakeInfo("Contoso.Foo", "1.1.0", Path.Combine(InstalledRoot, "Contoso.Foo", "1.1.0", "Contoso.Foo.dll"));
        var selection = new SelectedExtensionCandidate(bundledPinned, [installedNewer], ExtensionSelectionReason.Pinned);
        var entry = new ExtensionRegistryEntry { Version = bundledPinned.ResolvedVersion, Location = bundledPinned.Location };

        var candidates = ListExtensionsProcessor.BuildCandidates(selection, entry, InstalledRoot);

        Assert.Equal(2, candidates.Count);
        // Sort is by version desc regardless of which one was selected; the winner is named via
        // the row's SelectedVersion field (== entry.Version), not by position in the array.
        Assert.Equal("1.1.0", candidates[0].Version);
        Assert.Equal("Installed", candidates[0].Tier);
        Assert.Equal("1.0.0", candidates[1].Version);
        Assert.Equal("Bundled", candidates[1].Tier);
    }

    [Fact]
    public void BuildCandidates_ShouldFallBackToEntry_WhenSelectionIsNull()
    {
        // Legacy path: caller didn't populate selections dictionary. Reader still emits a
        // single-element candidates array derived from the collapsed entry.
        var entry = new ExtensionRegistryEntry
        {
            Version = "2.0.0",
            Location = Path.Combine(InstalledRoot, "Contoso.Foo", "2.0.0", "Contoso.Foo.dll"),
        };

        var candidates = ListExtensionsProcessor.BuildCandidates(selection: null, entry, InstalledRoot);

        Assert.Single(candidates);
        Assert.Equal("2.0.0", candidates[0].Version);
        Assert.Equal("Installed", candidates[0].Tier);
    }

    private static DiscoveredExtensionInfo MakeInfo(string packageId, string version, string location)
        => new(packageId, version, Verbs: [], Commands: new Dictionary<string, CommandMetadata>(), Location: location);
}
