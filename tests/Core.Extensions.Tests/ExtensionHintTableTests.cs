// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Extensions;

namespace Core.Extensions.Tests
{
    public class ExtensionHintTableTests
    {
        [Fact]
        public void TryGetPackageId_Verb_ShouldReturnTrue_ForExistingVerb()
        {
            var table = new ExtensionHintTable
            {
                Leaves = new Dictionary<string, ExtensionHint>(StringComparer.OrdinalIgnoreCase)
                {
                    ["vm"] = new() { PackageId = "Microsoft.Azure.Azclips.Extensions.Compute" },
                },
            };

            Assert.True(table.TryGetPackageId("vm", out var pkg));
            Assert.Equal("Microsoft.Azure.Azclips.Extensions.Compute", pkg);

            Assert.True(table.TryGetPackageId("VM", out _)); // case-insensitive
        }

        [Fact]
        public void TryGetPackageId_VerbAndPath_ShouldReturnTrue_ForLeafKey()
        {
            var table = new ExtensionHintTable
            {
                Leaves = new Dictionary<string, ExtensionHint>(StringComparer.OrdinalIgnoreCase)
                {
                    ["vm create"] = new() { PackageId = "Microsoft.Azure.Azclips.Extensions.Compute" },
                    ["vm scale"]  = new() { PackageId = "Microsoft.Azure.Azclips.Extensions.ComputeFuture" },
                },
            };

            Assert.True(table.TryGetPackageId("vm", "create", out var pkg));
            Assert.Equal("Microsoft.Azure.Azclips.Extensions.Compute", pkg);

            Assert.True(table.TryGetPackageId("VM", "SCALE", out var pkg2));
            Assert.Equal("Microsoft.Azure.Azclips.Extensions.ComputeFuture", pkg2);
        }

        [Fact]
        public void TryGetPackageId_VerbAndPath_ShouldFallBackToBareVerb_WhenPathEmpty()
        {
            var table = new ExtensionHintTable
            {
                Leaves = new Dictionary<string, ExtensionHint>(StringComparer.OrdinalIgnoreCase)
                {
                    ["login"] = new() { PackageId = "Microsoft.Azure.Azclips.Extensions.Accounts" },
                },
            };

            Assert.True(table.TryGetPackageId("login", "", out var pkg));
            Assert.Equal("Microsoft.Azure.Azclips.Extensions.Accounts", pkg);
        }

        [Fact]
        public void TryGetPackageId_ShouldReturnFalse_WhenMissing()
        {
            var table = new ExtensionHintTable();
            Assert.False(table.TryGetPackageId("nope", out var pkg1));
            Assert.Equal(string.Empty, pkg1);
            Assert.False(table.TryGetPackageId("nope", "either", out var pkg2));
            Assert.Equal(string.Empty, pkg2);
        }

        [Fact]
        public void Default_ShouldBeNonNull_WithLeavesInitialized()
        {
            var table = ExtensionHintTable.Default;
            Assert.NotNull(table);
            Assert.NotNull(table.Leaves);
        }

        [Fact]
        public void Deserialize_ShouldRoundTripCompatFields()
        {
            // Future feed entries can carry minCoreVersion/maxCoreVersion. Reader must surface
            // them so the vNext feed-side compat-aware filter can use them without a code change.
            const string json = """
                {
                  "schemaVersion": 1,
                  "generatedAt": "2026-05-28T10:00:00Z",
                  "sourceHostVersion": "1.2.0",
                  "leaves": {
                    "vm contoso-magic": {
                      "packageId": "Contoso.VmPreviewExt",
                      "minCoreVersion": "1.5.0",
                      "maxCoreVersion": null
                    }
                  }
                }
                """;
            var table = System.Text.Json.JsonSerializer.Deserialize<ExtensionHintTable>(json);
            Assert.NotNull(table);
            Assert.Equal("1.2.0", table!.SourceHostVersion);
            Assert.Equal(new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero), table.GeneratedAt);
            var hint = table.Leaves["vm contoso-magic"];
            Assert.Equal("Contoso.VmPreviewExt", hint.PackageId);
            Assert.Equal("1.5.0", hint.MinCoreVersion);
            Assert.Null(hint.MaxCoreVersion);
        }

        [Fact]
        public void Deserialize_ShouldIgnoreUnknownProperties()
        {
            // Tolerant parse: a future feed file may carry extras (e.g. signing metadata) that
            // older hosts don't understand. Don't choke.
            const string json = """
                {
                  "schemaVersion": 1,
                  "futureFlag": true,
                  "leaves": {
                    "vm": { "packageId": "Microsoft.Azure.Azclips.Extensions.Compute", "futurePerLeaf": 42 }
                  }
                }
                """;
            var table = System.Text.Json.JsonSerializer.Deserialize<ExtensionHintTable>(json);
            Assert.NotNull(table);
            Assert.True(table!.TryGetPackageId("vm", out var pkg));
            Assert.Equal("Microsoft.Azure.Azclips.Extensions.Compute", pkg);
        }
    }
}

