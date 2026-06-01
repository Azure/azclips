using Core.Extensions;
using Microsoft.Azure.Cirrus.Extensions.Extension.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Phase 10 #34: <c>extension add --name</c> must accept three input shapes — a verb (resolved
/// via the embedded hint catalog), a first-party short name (prefix-stripped), and a fully
/// qualified package id (passthrough). These tests pin the resolver's three branches.
/// </summary>
public class ExtensionAddNameResolutionTests
{
    private static ExtensionHintTable HintsWith(params (string key, string packageId)[] entries)
    {
        var table = new ExtensionHintTable();
        foreach (var (key, packageId) in entries)
        {
            table.Leaves[key] = new ExtensionHint { PackageId = packageId };
        }
        return table;
    }

    [Fact]
    public void ResolvePackageId_ShouldReturnHintedPackage_WhenNameMatchesVerb()
    {
        var hints = HintsWith(("vm", "Microsoft.Azure.Cirrus.Extensions.Compute"));

        var result = AddExtensionProcessor.ResolvePackageId("vm", hints);

        Assert.Equal("Microsoft.Azure.Cirrus.Extensions.Compute", result);
    }

    [Fact]
    public void ResolvePackageId_ShouldBeCaseInsensitive_OnHintLookup()
    {
        var hints = HintsWith(("vm", "Microsoft.Azure.Cirrus.Extensions.Compute"));

        var result = AddExtensionProcessor.ResolvePackageId("VM", hints);

        Assert.Equal("Microsoft.Azure.Cirrus.Extensions.Compute", result);
    }

    [Fact]
    public void ResolvePackageId_ShouldPrefixStrip_WhenNameMissesHintAndIsBare()
    {
        // "Resources" is a first-party short name, not a verb in the catalog.
        var hints = HintsWith(("vm", "Microsoft.Azure.Cirrus.Extensions.Compute"));

        var result = AddExtensionProcessor.ResolvePackageId("Resources", hints);

        Assert.Equal("Microsoft.Azure.Cirrus.Extensions.Resources", result);
    }

    [Fact]
    public void ResolvePackageId_ShouldPassthrough_WhenNameContainsDot()
    {
        // A dotted token is treated as a fully qualified package id, even if a verb-shaped key
        // happens to be present in the hint table. Don't second-guess the user.
        var hints = HintsWith(("Contoso.Foo", "Microsoft.Azure.Cirrus.Extensions.Compute"));

        var result = AddExtensionProcessor.ResolvePackageId("Contoso.Foo", hints);

        Assert.Equal("Contoso.Foo", result);
    }

    [Fact]
    public void ResolvePackageId_ShouldPassthrough_WhenNameIsAlreadyFirstPartyPackageId()
    {
        var hints = new ExtensionHintTable();

        var result = AddExtensionProcessor.ResolvePackageId(
            "Microsoft.Azure.Cirrus.Extensions.Compute",
            hints);

        Assert.Equal("Microsoft.Azure.Cirrus.Extensions.Compute", result);
    }

    [Fact]
    public void ResolvePackageId_ShouldPreferHint_OverPrefixStrip_WhenBothCouldApply()
    {
        // Edge case: a future hint entry whose key collides with a first-party short name. Hint
        // wins because it represents a deliberate ownership claim from a build-time validated
        // catalog; prefix-strip is a fallback heuristic.
        var hints = HintsWith(("Compute", "Contoso.Compute.Override"));

        var result = AddExtensionProcessor.ResolvePackageId("Compute", hints);

        Assert.Equal("Contoso.Compute.Override", result);
    }

    [Fact]
    public void ResolvePackageId_ShouldThrow_OnEmptyName()
    {
        var hints = new ExtensionHintTable();

        Assert.Throws<ArgumentException>(() => AddExtensionProcessor.ResolvePackageId("", hints));
    }
}
