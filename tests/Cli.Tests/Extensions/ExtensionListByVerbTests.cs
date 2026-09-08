// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Extension.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// <c>extension list --by-verb</c> regroups contributors verb-headed, duplicating
/// multi-namespace extensions under each verb they actually contribute to. Sort order (verbs
/// then packageId, ordinal-ignore-case) is contract.
/// </summary>
public class ExtensionListByVerbTests
{
    private static LiveContributor C(string packageId, params string[] namespaces) =>
        new(packageId,
            Version: "1.0.0",
            SelectedVersion: "1.0.0",
            Pinned: false,
            Source: null,
            InstalledAt: DateTimeOffset.UtcNow,
            Location: null,
            Namespaces: namespaces,
            Candidates: [new CandidateInfo("1.0.0", "Bundled", null)]);

    [Fact]
    public void PivotByVerb_ShouldGroupSingleNamespaceContributor()
    {
        var result = ListExtensionsProcessor.PivotByVerb(new[] { C("Microsoft.Azure.Azclips.Extensions.Resources", "group") });

        Assert.Single(result);
        Assert.True(result.ContainsKey("group"));
        Assert.Equal("Microsoft.Azure.Azclips.Extensions.Resources", result["group"].Single().PackageId);
    }

    [Fact]
    public void PivotByVerb_ShouldDuplicateMultiNamespaceContributorUnderEachVerb()
    {
        var result = ListExtensionsProcessor.PivotByVerb(new[] { C("Microsoft.Azure.Azclips.Extensions.Compute", "vm", "disk") });

        Assert.Equal(2, result.Count);
        Assert.Equal("Microsoft.Azure.Azclips.Extensions.Compute", result["vm"].Single().PackageId);
        Assert.Equal("Microsoft.Azure.Azclips.Extensions.Compute", result["disk"].Single().PackageId);
    }

    [Fact]
    public void PivotByVerb_ShouldOrderVerbsAlphabeticallyOrdinalIgnoreCase()
    {
        var result = ListExtensionsProcessor.PivotByVerb(new[]
        {
            C("Microsoft.Azure.Azclips.Extensions.Resources", "group"),
            C("Microsoft.Azure.Azclips.Extensions.Compute", "vm", "disk"),
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
            C("Microsoft.Azure.Azclips.Extensions.Compute", "vm"),
        });

        var vm = result["vm"].Select(r => r.PackageId).ToArray();
        Assert.Equal(new[] { "Alpha.Vm", "Microsoft.Azure.Azclips.Extensions.Compute", "Zeta.Vm" }, vm);
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

    [Fact]
    public void PivotByVerb_ShouldPropagateLocation_FromContributorToRow()
    {
        // Location on the input contributor must land on every emitted row.
        const string bundledLocation = @"C:\Program Files\azclips\Extensions\Microsoft.Azure.Azclips.Extensions.Accounts\Microsoft.Azure.Azclips.Extensions.Accounts.dll";
        const string installedLocation = @"C:\Users\example\.azclips\extensions\Microsoft.Azure.Azclips.Extensions.Compute\1.2.3\Microsoft.Azure.Azclips.Extensions.Compute.dll";

        var bundled = new LiveContributor(
            PackageId: "Microsoft.Azure.Azclips.Extensions.Accounts",
            Version: "0.1.0",
            SelectedVersion: "0.1.0",
            Pinned: false,
            Source: null,
            InstalledAt: DateTimeOffset.UtcNow,
            Location: bundledLocation,
            Namespaces: new[] { "login" },
            Candidates: [new CandidateInfo("0.1.0", "Bundled", bundledLocation)]);

        var installed = new LiveContributor(
            PackageId: "Microsoft.Azure.Azclips.Extensions.Compute",
            Version: "1.2.3",
            SelectedVersion: "1.2.3",
            Pinned: false,
            Source: null,
            InstalledAt: DateTimeOffset.UtcNow,
            Location: installedLocation,
            Namespaces: new[] { "vm" },
            Candidates: [new CandidateInfo("1.2.3", "Installed", installedLocation)]);

        var result = ListExtensionsProcessor.PivotByVerb(new[] { bundled, installed });

        Assert.Equal(bundledLocation, result["login"].Single().Location);
        Assert.Equal(installedLocation, result["vm"].Single().Location);
    }

    [Fact]
    public void PivotByVerb_ShouldPreserveNullLocation_WhenLoaderCannotReportPath()
    {
        // Null Location (e.g. named-extension lookup path) must round-trip, not be synthesized.
        var result = ListExtensionsProcessor.PivotByVerb(new[] { C("Some.Pkg", "verb") });

        Assert.Null(result["verb"].Single().Location);
    }
}
