using Microsoft.Azure.Cirrus.Extensions.Extension.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Phase 10 #31 regression: <c>extension list --by-verb</c> regroups contributors verb-headed,
/// duplicating multi-namespace extensions under each verb they actually contribute to. Sort
/// order (verbs then packageId, ordinal-ignore-case) is contract.
/// </summary>
public class ExtensionListByVerbTests
{
    private static LiveContributor C(string packageId, params string[] namespaces) =>
        new(packageId, Version: "1.0.0", Pinned: false, Source: null, InstalledAt: DateTimeOffset.UtcNow, Namespaces: namespaces);

    [Fact]
    public void PivotByVerb_ShouldGroupSingleNamespaceContributor()
    {
        var result = ListExtensionsProcessor.PivotByVerb(new[] { C("Microsoft.Azure.Cirrus.Extensions.Resources", "group") });

        Assert.Single(result);
        Assert.True(result.ContainsKey("group"));
        Assert.Equal("Microsoft.Azure.Cirrus.Extensions.Resources", result["group"].Single().PackageId);
    }

    [Fact]
    public void PivotByVerb_ShouldDuplicateMultiNamespaceContributorUnderEachVerb()
    {
        var result = ListExtensionsProcessor.PivotByVerb(new[] { C("Microsoft.Azure.Cirrus.Extensions.Compute", "vm", "disk") });

        Assert.Equal(2, result.Count);
        Assert.Equal("Microsoft.Azure.Cirrus.Extensions.Compute", result["vm"].Single().PackageId);
        Assert.Equal("Microsoft.Azure.Cirrus.Extensions.Compute", result["disk"].Single().PackageId);
    }

    [Fact]
    public void PivotByVerb_ShouldOrderVerbsAlphabeticallyOrdinalIgnoreCase()
    {
        var result = ListExtensionsProcessor.PivotByVerb(new[]
        {
            C("Microsoft.Azure.Cirrus.Extensions.Resources", "group"),
            C("Microsoft.Azure.Cirrus.Extensions.Compute", "vm", "disk"),
        });

        Assert.Equal(new[] { "disk", "group", "vm" }, result.Keys.ToArray());
    }

    [Fact]
    public void PivotByVerb_ShouldOrderContributorsWithinVerbByPackageId()
    {
        var result = ListExtensionsProcessor.PivotByVerb(new[]
        {
            C("Zeta.Vm", "vm"),
            C("Alpha.Vm", "vm"),
            C("Microsoft.Azure.Cirrus.Extensions.Compute", "vm"),
        });

        var vm = result["vm"].Select(r => r.PackageId).ToArray();
        Assert.Equal(new[] { "Alpha.Vm", "Microsoft.Azure.Cirrus.Extensions.Compute", "Zeta.Vm" }, vm);
    }

    [Fact]
    public void PivotByVerb_ShouldReturnEmpty_WhenNoContributors()
    {
        var result = ListExtensionsProcessor.PivotByVerb(Array.Empty<LiveContributor>());

        Assert.Empty(result);
    }

    [Fact]
    public void PivotByVerb_ShouldOmitVerbHeading_WhenContributorListsNoNamespaces()
    {
        // A contributor with an empty Namespaces list (defensive: shouldn't happen in practice
        // but the projection must not crash or invent a phantom verb).
        var result = ListExtensionsProcessor.PivotByVerb(new[] { C("Orphan.Pkg" /* no namespaces */) });

        Assert.Empty(result);
    }
}
