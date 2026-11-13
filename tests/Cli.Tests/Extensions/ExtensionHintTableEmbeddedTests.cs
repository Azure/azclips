// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Extensions;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Assertions on the embedded <c>extension-hints.json</c> shipped via Core.Cli.
/// Lives in <c>Cli.Tests</c> rather than <c>Core.Extensions.Tests</c> because the embedded
/// resource is only present in assemblies that reference Core.Cli — which Cli.Tests does
/// transitively, but Core.Extensions.Tests does not.
/// </summary>
public class ExtensionHintTableEmbeddedTests
{
    [Fact]
    public void Default_ShouldCarryGeneratorMetadata()
    {
        // The generator stamps generatedAt + sourceHostVersion so the vNext feed half can reason
        // about cache freshness without a schema bump.
        var table = ExtensionHintTable.Default;
        Assert.NotNull(table.GeneratedAt);
        Assert.False(string.IsNullOrWhiteSpace(table.SourceHostVersion));
    }

    [Fact]
    public void Default_ShouldKeepPreciseStorageHints_WithoutAnAmbiguousGroupOwner()
    {
        var table = ExtensionHintTable.Default;

        Assert.False(table.TryGetPackageId("storage", out _));
        Assert.True(table.TryGetPackageId("storage", "account list", out var accountPackage));
        Assert.Equal("Microsoft.Azure.Azclips.Extensions.Storage", accountPackage);
        Assert.True(table.TryGetPackageId("storage", "blob", out var blobPackage));
        Assert.Equal("Microsoft.Azure.Azclips.Extensions.StorageData.Blobs", blobPackage);
    }

    [Fact]
    public void Default_ShouldEmitObjectShapedLeafEntries()
    {
        // Leaves are objects (not bare strings) so optional compat fields can be added later
        // without re-bumping schemaVersion. Spot-check the well-known "vm" leaf.
        var table = ExtensionHintTable.Default;
        Assert.True(table.Leaves.TryGetValue("vm", out var vm),
            "Expected 'vm' leaf in embedded hints (Compute extension).");
        Assert.False(string.IsNullOrWhiteSpace(vm!.PackageId));
        // v1 first-party output leaves compat fields unset.
        Assert.Null(vm.MinCoreVersion);
        Assert.Null(vm.MaxCoreVersion);
    }
}
