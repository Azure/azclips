// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Extensions;
using Microsoft.Azure.Azclips.Extensions.Extension.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// <c>extension add --name</c> accepts only package identifiers — a fully-qualified package id
/// (passthrough) or a first-party short name (prefix-stripped). A bare token matching a verb
/// in the hint catalog is rejected with a usage error pointing at the owning package id.
/// Verbs are sub-units of a package; accepting them as install identifiers would silently pull
/// in unrelated sibling verbs (Compute ships <c>vm</c> and <c>disk</c>). Symmetric with
/// remove/show/update/pin/unpin.
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
    public void ResolvePackageId_ShouldThrowUsageError_WhenNameIsAVerb()
    {
        var hints = HintsWith(("vm", "Microsoft.Azure.Azclips.Extensions.Compute"));

        var ex = Assert.Throws<UsageErrorException>(
            () => AddExtensionProcessor.ResolvePackageId("vm", hints));

        Assert.Equal("UsageError", ex.Code);
        Assert.Contains("--name Compute", ex.Message);
        Assert.Contains("Microsoft.Azure.Azclips.Extensions.Compute", ex.Message);
    }

    [Fact]
    public void ResolvePackageId_ShouldThrowUsageError_WhenNameIsAVerb_CaseInsensitive()
    {
        var hints = HintsWith(("vm", "Microsoft.Azure.Azclips.Extensions.Compute"));

        var ex = Assert.Throws<UsageErrorException>(
            () => AddExtensionProcessor.ResolvePackageId("VM", hints));

        Assert.Equal("UsageError", ex.Code);
    }

    [Fact]
    public void ResolvePackageId_ShouldPrefixStrip_WhenNameMissesHintAndIsBare()
    {
        // "Resources" is a first-party short name, not a verb in the catalog.
        var hints = HintsWith(("vm", "Microsoft.Azure.Azclips.Extensions.Compute"));

        var result = AddExtensionProcessor.ResolvePackageId("Resources", hints);

        Assert.Equal("Microsoft.Azure.Azclips.Extensions.Resources", result);
    }

    [Fact]
    public void ResolvePackageId_ShouldPassthrough_WhenNameContainsDot()
    {
        // A dotted token is a fully qualified package id and is trusted as-is, even if a
        // collision-shaped key is in the hint catalog. Don't second-guess the user.
        var hints = HintsWith(("Contoso.Foo", "Microsoft.Azure.Azclips.Extensions.Compute"));

        var result = AddExtensionProcessor.ResolvePackageId("Contoso.Foo", hints);

        Assert.Equal("Contoso.Foo", result);
    }

    [Fact]
    public void ResolvePackageId_ShouldPassthrough_WhenNameIsAlreadyFirstPartyPackageId()
    {
        var hints = new ExtensionHintTable();

        var result = AddExtensionProcessor.ResolvePackageId(
            "Microsoft.Azure.Azclips.Extensions.Compute",
            hints);

        Assert.Equal("Microsoft.Azure.Azclips.Extensions.Compute", result);
    }

    [Fact]
    public void ResolvePackageId_ShouldThrow_OnEmptyName()
    {
        var hints = new ExtensionHintTable();

        Assert.Throws<ArgumentException>(() => AddExtensionProcessor.ResolvePackageId("", hints));
    }
}
